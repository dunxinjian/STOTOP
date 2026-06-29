using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class VoucherAutoService : IVoucherAutoService
{
    private readonly IRepository<FinVoucherRule> _ruleRepository;
    private readonly IRepository<FinBankTransaction> _transactionRepository;
    private readonly IRepository<FinPaymentChannel> _channelRepository;

    public VoucherAutoService(
        IRepository<FinVoucherRule> ruleRepository,
        IRepository<FinBankTransaction> transactionRepository,
        IRepository<FinPaymentChannel> channelRepository)
    {
        _ruleRepository = ruleRepository;
        _transactionRepository = transactionRepository;
        _channelRepository = channelRepository;
    }

    #region 凭证规则 CRUD

    public async Task<PagedResult<VoucherRuleDto>> GetRulesAsync(VoucherRuleQueryRequest request)
    {
        var query = _ruleRepository.Query();

        if (request.ChannelId.HasValue)
        {
            query = query.Where(r => r.FChannelId == request.ChannelId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(r => r.FStatus == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(r => r.FRuleName.Contains(keyword));
        }

        var total = await query.CountAsync();

        // 联查渠道名称
        var items = await query
            .GroupJoin(
                _channelRepository.Query(),
                r => r.FChannelId,
                c => c.FID,
                (r, channels) => new { Rule = r, Channels = channels })
            .SelectMany(
                x => x.Channels.DefaultIfEmpty(),
                (x, c) => new { x.Rule, ChannelName = c != null ? c.FName : null })
            .OrderBy(x => x.Rule.FPriority)
            .ThenByDescending(x => x.Rule.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<VoucherRuleDto>
        {
            Items = items.Select(x => MapToRuleDto(x.Rule, x.ChannelName)).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<List<VoucherRuleDto>> GetRulesByPriorityAsync()
    {
        var items = await _ruleRepository.Query()
            .Where(r => r.FStatus == 1)
            .GroupJoin(
                _channelRepository.Query(),
                r => r.FChannelId,
                c => c.FID,
                (r, channels) => new { Rule = r, Channels = channels })
            .SelectMany(
                x => x.Channels.DefaultIfEmpty(),
                (x, c) => new { x.Rule, ChannelName = c != null ? c.FName : null })
            .OrderBy(x => x.Rule.FPriority)
            .ToListAsync();

        return items.Select(x => MapToRuleDto(x.Rule, x.ChannelName)).ToList();
    }

    public async Task<VoucherRuleDto?> GetRuleByIdAsync(long id)
    {
        var result = await _ruleRepository.Query()
            .Where(r => r.FID == id)
            .GroupJoin(
                _channelRepository.Query(),
                r => r.FChannelId,
                c => c.FID,
                (r, channels) => new { Rule = r, Channels = channels })
            .SelectMany(
                x => x.Channels.DefaultIfEmpty(),
                (x, c) => new { x.Rule, ChannelName = c != null ? c.FName : null })
            .FirstOrDefaultAsync();

        return result == null ? null : MapToRuleDto(result.Rule, result.ChannelName);
    }

    public async Task<VoucherRuleDto> CreateRuleAsync(CreateVoucherRuleRequest request, string? operatorName)
    {
        var rule = new FinVoucherRule
        {
            FRuleName = request.RuleName,
            FChannelId = request.ChannelId,
            FMatchCondition = request.MatchCondition,
            FDebitAccount = request.DebitAccount,
            FCreditAccount = request.CreditAccount,
            FSummaryTemplate = request.SummaryTemplate,
            FPriority = request.Priority,
            FStatus = 1,
            FCreatorName = operatorName,
            FCreatedTime = DateTime.Now
        };

        await _ruleRepository.AddAsync(rule);

        // 获取渠道名称
        string? channelName = null;
        if (rule.FChannelId.HasValue)
        {
            var channel = await _channelRepository.GetByIdAsync(rule.FChannelId.Value);
            channelName = channel?.FName;
        }

        return MapToRuleDto(rule, channelName);
    }

    public async Task<VoucherRuleDto?> UpdateRuleAsync(long id, UpdateVoucherRuleRequest request, string? operatorName)
    {
        var rule = await _ruleRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (rule == null) return null;

        rule.FRuleName = request.RuleName;
        rule.FChannelId = request.ChannelId;
        rule.FMatchCondition = request.MatchCondition;
        rule.FDebitAccount = request.DebitAccount;
        rule.FCreditAccount = request.CreditAccount;
        rule.FSummaryTemplate = request.SummaryTemplate;
        rule.FPriority = request.Priority;
        rule.FStatus = request.Status;
        rule.FUpdaterName = operatorName;
        rule.FUpdatedTime = DateTime.Now;

        await _ruleRepository.UpdateAsync(rule);
        return await GetRuleByIdAsync(id);
    }

    public async Task<bool> DeleteRuleAsync(long id)
    {
        var rule = await _ruleRepository.GetByIdAsync(id);
        if (rule == null) return false;

        await _ruleRepository.DeleteAsync(id);
        return true;
    }

    #endregion

    #region 自动生成凭证草稿

    /// <summary>
    /// 自动生成凭证草稿。
    /// 当前银行流水缺少账套和会计期间上下文，命中规则时只返回明确错误，避免假标记为已生成。
    /// </summary>
    public async Task<VoucherGenerateResult> GenerateVoucherDraftAsync(string? operatorName)
    {
        var result = new VoucherGenerateResult();

        // 获取已匹配但未生成凭证的流水
        var transactions = await _transactionRepository.Query()
            .AsTracking()
            .Where(t => t.FMatchStatus == 1 && t.FVoucherId == null)
            .ToListAsync();

        // 获取启用的规则（按优先级排序）
        var rules = await _ruleRepository.Query()
            .Where(r => r.FStatus == 1)
            .OrderBy(r => r.FPriority)
            .ToListAsync();

        result.TotalProcessed = transactions.Count;

        foreach (var transaction in transactions)
        {
            try
            {
                // 按优先级遍历规则，找到第一个匹配的规则
                var matchedRule = FindMatchingRule(rules, transaction);

                if (matchedRule != null)
                {
                    result.Errors.Add($"流水 {transaction.FTransactionNo} 命中规则 {matchedRule.FRuleName}，但自动生成凭证缺少账套/会计期间上下文，已跳过");
                    result.SkippedCount++;
                }
                else
                {
                    result.SkippedCount++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"流水 {transaction.FTransactionNo} 处理失败: {ex.Message}");
                result.SkippedCount++;
            }
        }

        return result;
    }

    /// <summary>
    /// 按规则匹配流水（简化版：匹配渠道和基本条件）
    /// </summary>
    private static FinVoucherRule? FindMatchingRule(List<FinVoucherRule> rules, FinBankTransaction transaction)
    {
        foreach (var rule in rules)
        {
            // 渠道匹配（规则渠道为空表示全渠道）
            if (rule.FChannelId.HasValue && rule.FChannelId.Value != transaction.FChannelId)
                continue;

            // 当前实现只做渠道匹配；更复杂的匹配条件可继续解析 FMatchCondition JSON：
            // { "counterpartNameContains": "xxx", "direction": 1, "summaryContains": "xxx" }

            return rule;
        }

        return null;
    }

    #endregion

    #region 统计接口

    public async Task<FundStatisticsDto> GetStatisticsAsync()
    {
        var totalImported = await _transactionRepository.Query().CountAsync();
        var matchedCount = await _transactionRepository.Query().CountAsync(t => t.FMatchStatus == 1);
        var unmatchedCount = await _transactionRepository.Query().CountAsync(t => t.FMatchStatus == 0);
        var skipMatchedCount = await _transactionRepository.Query().CountAsync(t => t.FMatchStatus == 2);
        var voucherGeneratedCount = await _transactionRepository.Query().CountAsync(t => t.FVoucherId != null);

        return new FundStatisticsDto
        {
            TotalImported = totalImported,
            MatchedCount = matchedCount,
            UnmatchedCount = unmatchedCount,
            SkipMatchedCount = skipMatchedCount,
            VoucherGeneratedCount = voucherGeneratedCount,
            MatchRate = totalImported > 0 ? Math.Round((decimal)matchedCount / totalImported * 100, 2) : 0,
            VoucherRate = totalImported > 0 ? Math.Round((decimal)voucherGeneratedCount / totalImported * 100, 2) : 0
        };
    }

    #endregion

    #region Mapping

    private static VoucherRuleDto MapToRuleDto(FinVoucherRule entity, string? channelName)
    {
        return new VoucherRuleDto
        {
            Id = entity.FID,
            RuleName = entity.FRuleName,
            ChannelId = entity.FChannelId,
            ChannelName = channelName,
            MatchCondition = entity.FMatchCondition,
            DebitAccount = entity.FDebitAccount,
            CreditAccount = entity.FCreditAccount,
            SummaryTemplate = entity.FSummaryTemplate,
            Priority = entity.FPriority,
            Status = entity.FStatus,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime,
            UpdaterName = entity.FUpdaterName,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    #endregion
}
