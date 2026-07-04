using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Constants;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

/// <summary>
/// 账套规则服务（一账套一行）。
/// 注意：FinAccountSetRule 实现 IAccountSetScoped 仅是标记接口、无自动过滤器，
/// 账套维度必须逐查询手写 Where(FAccountSetId == accountSetId)；租户维度由全局过滤器 fail-closed 兜底。
/// </summary>
public class AccountSetRuleService : IAccountSetRuleService
{
    private readonly IRepository<FinAccountSetRule> _ruleRepository;
    private readonly IRepository<FinAccount> _accountRepository;
    private readonly OperationLogService _operationLogService;

    public AccountSetRuleService(
        IRepository<FinAccountSetRule> ruleRepository,
        IRepository<FinAccount> accountRepository,
        OperationLogService operationLogService)
    {
        _ruleRepository = ruleRepository;
        _accountRepository = accountRepository;
        _operationLogService = operationLogService;
    }

    public async Task<FinAccountSetRule?> GetByAccountSetAsync(long accountSetId)
    {
        if (accountSetId <= 0) return null;
        return await _ruleRepository.Query()
            .FirstOrDefaultAsync(r => r.FAccountSetId == accountSetId);
    }

    public async Task<AccountSetRuleDto> GetDtoAsync(long accountSetId)
    {
        var rule = await GetByAccountSetAsync(accountSetId);
        return new AccountSetRuleDto
        {
            FAccountSetId = accountSetId,
            FRequireAuditSeparation = rule?.FRequireAuditSeparation ?? false,
            FProfitAccountCode = rule?.FProfitAccountCode,
            FRetainedAccountCode = rule?.FRetainedAccountCode,
            FEnabledVoucherWords = ParseEnabledWords(rule?.FEnabledVoucherWords).ToList()
        };
    }

    public async Task<string[]> GetEnabledVoucherWordsAsync(long accountSetId)
    {
        var rule = await GetByAccountSetAsync(accountSetId);
        return ParseEnabledWords(rule?.FEnabledVoucherWords);
    }

    public async Task<(string profitCode, string retainedCode)> GetClosingAccountCodesAsync(long accountSetId)
    {
        var rule = await GetByAccountSetAsync(accountSetId);
        var profitCode = string.IsNullOrWhiteSpace(rule?.FProfitAccountCode)
            ? AccountSetRuleDefaults.ProfitAccountCode : rule.FProfitAccountCode.Trim();
        var retainedCode = string.IsNullOrWhiteSpace(rule?.FRetainedAccountCode)
            ? AccountSetRuleDefaults.RetainedAccountCode : rule.FRetainedAccountCode.Trim();
        return (profitCode, retainedCode);
    }

    public async Task<AccountSetRuleDto> UpsertAsync(long accountSetId, UpdateAccountSetRuleRequest request, string operatorName)
    {
        if (accountSetId <= 0)
            throw new InvalidOperationException("请先选择账套");

        // 凭证字：必须是全集合法子集；强制包含"记"（系统默认字，自动凭证/默认值依赖）
        var words = (request.FEnabledVoucherWords ?? new List<string>())
            .Select(w => w?.Trim() ?? string.Empty)
            .Where(w => w.Length > 0)
            .Distinct()
            .ToList();
        var invalid = words.Where(w => !VoucherWord.AllWords.Contains(w)).ToList();
        if (invalid.Count > 0)
            throw new InvalidOperationException($"无效的凭证字：{string.Join("、", invalid)}（合法值：{string.Join("/", VoucherWord.AllWords)}）");
        if (!words.Contains(VoucherWord.Ji))
            words.Insert(0, VoucherWord.Ji);
        // 按全集固定顺序存储，避免配置行序随提交顺序漂移
        words = VoucherWord.AllWords.Where(words.Contains).ToList();

        // 结转科目编码：空=回退默认；非空须在当前账套存在（防结账时解析失败）
        var profitCode = NormalizeCode(request.FProfitAccountCode);
        var retainedCode = NormalizeCode(request.FRetainedAccountCode);
        await EnsureAccountExistsAsync(accountSetId, profitCode, "本年利润结转科目");
        await EnsureAccountExistsAsync(accountSetId, retainedCode, "未分配利润结转科目");

        var rule = await _ruleRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FAccountSetId == accountSetId);

        var enabledJson = JsonSerializer.Serialize(words);
        if (rule == null)
        {
            rule = new FinAccountSetRule
            {
                FAccountSetId = accountSetId,
                FRequireAuditSeparation = request.FRequireAuditSeparation,
                FProfitAccountCode = profitCode,
                FRetainedAccountCode = retainedCode,
                FEnabledVoucherWords = enabledJson,
                FStatus = 1,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
                // FTenantId/FOrgId 不手赋值：租户由 DbContext 回填，组织列恒 0
            };
            await _ruleRepository.AddAsync(rule);
        }
        else
        {
            rule.FRequireAuditSeparation = request.FRequireAuditSeparation;
            rule.FProfitAccountCode = profitCode;
            rule.FRetainedAccountCode = retainedCode;
            rule.FEnabledVoucherWords = enabledJson;
            rule.FUpdatedTime = DateTime.Now;
            await _ruleRepository.UpdateAsync(rule);
        }

        await _operationLogService.LogAsync(
            accountSetId, "账套规则", "保存",
            $"保存账套规则：制单审核分离={(request.FRequireAuditSeparation ? "开" : "关")}，" +
            $"本年利润科目={profitCode ?? "(默认" + AccountSetRuleDefaults.ProfitAccountCode + ")"}，" +
            $"未分配利润科目={retainedCode ?? "(默认" + AccountSetRuleDefaults.RetainedAccountCode + ")"}，" +
            $"启用凭证字={string.Join("/", words)}，操作人={operatorName}",
            rule.FID, "账套规则");

        return await GetDtoAsync(accountSetId);
    }

    /// <summary>解析启用凭证字 JSON：空/无效/无合法值 → 全集；有值时过滤到合法子集并强制含"记"。</summary>
    private static string[] ParseEnabledWords(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return VoucherWord.AllWords;
        try
        {
            var words = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            var valid = VoucherWord.AllWords.Where(words.Contains).ToList();
            if (valid.Count == 0) return VoucherWord.AllWords;
            if (!valid.Contains(VoucherWord.Ji)) valid.Insert(0, VoucherWord.Ji);
            return valid.ToArray();
        }
        catch (JsonException)
        {
            return VoucherWord.AllWords; // 脏数据回退全集，保持现状行为
        }
    }

    private static string? NormalizeCode(string? code)
    {
        var trimmed = code?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private async Task EnsureAccountExistsAsync(long accountSetId, string? code, string label)
    {
        if (code == null) return;
        var exists = await _accountRepository.Query()
            .AnyAsync(a => a.FCode == code && a.FAccountSetId == accountSetId);
        if (!exists)
            throw new InvalidOperationException($"{label}编码 {code} 在当前账套不存在");
    }
}
