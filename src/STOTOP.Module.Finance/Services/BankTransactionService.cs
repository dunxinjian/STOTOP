using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class BankTransactionService : IBankTransactionService
{
    private readonly IRepository<FinPaymentChannel> _channelRepository;
    private readonly IRepository<FinBankTransaction> _transactionRepository;

    public BankTransactionService(
        IRepository<FinPaymentChannel> channelRepository,
        IRepository<FinBankTransaction> transactionRepository)
    {
        _channelRepository = channelRepository;
        _transactionRepository = transactionRepository;
    }

    #region 交易渠道 CRUD

    public async Task<PagedResult<BankChannelDto>> GetChannelsAsync(BankChannelQueryRequest request)
    {
        var query = _channelRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(c => c.FName.Contains(keyword) || (c.FAccountNo != null && c.FAccountNo.Contains(keyword)));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(c => c.FStatus == request.Status.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<BankChannelDto>
        {
            Items = items.Select(MapToChannelDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<List<BankChannelDto>> GetAllEnabledChannelsAsync()
    {
        var channels = await _channelRepository.Query()
            .Where(c => c.FStatus == 1)
            .OrderBy(c => c.FName)
            .ToListAsync();

        return channels.Select(MapToChannelDto).ToList();
    }

    public async Task<BankChannelDto?> GetChannelByIdAsync(long id)
    {
        var channel = await _channelRepository.GetByIdAsync(id);
        return channel == null ? null : MapToChannelDto(channel);
    }

    public async Task<BankChannelDto> CreateChannelAsync(CreateBankChannelRequest request, string? operatorName)
    {
        var channel = new FinPaymentChannel
        {
            FName = request.Name,
            FType = request.Type,
            FAccountNo = request.AccountNo,
            FBankName = request.BankName,
            FImportTemplate = request.ImportTemplate,
            FStatus = 1,
            FCreatorName = operatorName,
            FCreatedTime = DateTime.Now
        };

        await _channelRepository.AddAsync(channel);
        return MapToChannelDto(channel);
    }

    public async Task<BankChannelDto?> UpdateChannelAsync(long id, UpdateBankChannelRequest request, string? operatorName)
    {
        var channel = await _channelRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == id);

        if (channel == null) return null;

        channel.FName = request.Name;
        channel.FType = request.Type;
        channel.FAccountNo = request.AccountNo;
        channel.FBankName = request.BankName;
        channel.FImportTemplate = request.ImportTemplate;
        channel.FStatus = request.Status;
        channel.FUpdaterName = operatorName;
        channel.FUpdatedTime = DateTime.Now;

        await _channelRepository.UpdateAsync(channel);
        return MapToChannelDto(channel);
    }

    public async Task<bool> DeleteChannelAsync(long id)
    {
        var channel = await _channelRepository.GetByIdAsync(id);
        if (channel == null) return false;

        // 检查是否有关联流水
        var hasTransactions = await _transactionRepository.Query()
            .AnyAsync(t => t.FChannelId == id);
        if (hasTransactions)
        {
            throw new InvalidOperationException("该渠道下存在银行流水，无法删除");
        }

        await _channelRepository.DeleteAsync(id);
        return true;
    }

    #endregion

    #region 流水导入

    public async Task<BankTransactionImportResult> ImportTransactionsAsync(BankTransactionImportRequest request, string? operatorName)
    {
        var batchId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var importedCount = 0;
        var duplicateCount = 0;

        foreach (var item in request.Items)
        {
            // 按交易流水号去重
            var exists = await _transactionRepository.Query()
                .AnyAsync(t => t.FTransactionNo == item.TransactionNo);

            if (exists)
            {
                duplicateCount++;
                continue;
            }

            var transaction = new FinBankTransaction
            {
                FChannelId = request.ChannelId,
                FTransactionDate = item.TransactionDate,
                FTransactionNo = item.TransactionNo,
                FCounterpartAccount = item.CounterpartAccount,
                FCounterpartName = item.CounterpartName,
                FDirection = item.Direction,
                FAmount = item.Amount,
                FBalance = item.Balance,
                FSummary = item.Summary,
                FRemark = item.Remark,
                FImportBatchId = batchId,
                FMatchStatus = 0,
                FCreatorName = operatorName,
                FCreatedTime = DateTime.Now
            };

            await _transactionRepository.AddAsync(transaction);
            importedCount++;
        }

        return new BankTransactionImportResult
        {
            TotalReceived = request.Items.Count,
            ImportedCount = importedCount,
            DuplicateCount = duplicateCount
        };
    }

    #endregion

    #region 流水查询

    public async Task<PagedResult<BankTransactionDto>> GetTransactionsAsync(BankTransactionQueryRequest request)
    {
        var query = _transactionRepository.Query();

        if (request.ChannelId.HasValue)
        {
            query = query.Where(t => t.FChannelId == request.ChannelId.Value);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(t => t.FTransactionDate >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(t => t.FTransactionDate <= request.EndDate.Value);
        }

        if (request.Direction.HasValue)
        {
            query = query.Where(t => t.FDirection == request.Direction.Value);
        }

        if (request.MatchStatus.HasValue)
        {
            query = query.Where(t => t.FMatchStatus == request.MatchStatus.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CounterpartName))
        {
            var name = request.CounterpartName.Trim();
            query = query.Where(t => t.FCounterpartName != null && t.FCounterpartName.Contains(name));
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(t =>
                t.FTransactionNo.Contains(keyword) ||
                (t.FCounterpartName != null && t.FCounterpartName.Contains(keyword)) ||
                (t.FSummary != null && t.FSummary.Contains(keyword)));
        }

        var total = await query.CountAsync();

        // 联查渠道名称
        var items = await query
            .Join(
                _channelRepository.Query(),
                t => t.FChannelId,
                c => c.FID,
                (t, c) => new { Transaction = t, ChannelName = c.FName })
            .OrderByDescending(x => x.Transaction.FTransactionDate)
            .ThenByDescending(x => x.Transaction.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<BankTransactionDto>
        {
            Items = items.Select(x => MapToTransactionDto(x.Transaction, x.ChannelName)).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<BankTransactionDto?> GetTransactionByIdAsync(long id)
    {
        var result = await _transactionRepository.Query()
            .Where(t => t.FID == id)
            .Join(
                _channelRepository.Query(),
                t => t.FChannelId,
                c => c.FID,
                (t, c) => new { Transaction = t, ChannelName = c.FName })
            .FirstOrDefaultAsync();

        return result == null ? null : MapToTransactionDto(result.Transaction, result.ChannelName);
    }

    #endregion

    #region 智能匹配

    public async Task<AutoMatchResult> AutoMatchAsync()
    {
        // 获取所有未匹配的流水
        var unmatchedTransactions = await _transactionRepository.Query()
            .AsTracking()
            .Where(t => t.FMatchStatus == 0)
            .ToListAsync();

        var matchedCount = 0;

        foreach (var transaction in unmatchedTransactions)
        {
            if (string.IsNullOrWhiteSpace(transaction.FCounterpartName))
                continue;

            // 简化版匹配：查找同名同金额的其他未匹配流水（反向）
            // 例如：收入流水匹配对应的支出业务
            // 这里预留匹配逻辑，实际匹配需要根据业务单据表来做
            // 当前简化实现：标记为已匹配（待后续完善具体业务匹配逻辑）

            // TODO: 实际业务匹配逻辑 — 按对方户名+金额匹配应收/应付单据
            // 当前仅作为框架预留
        }

        return new AutoMatchResult
        {
            TotalProcessed = unmatchedTransactions.Count,
            MatchedCount = matchedCount,
            UnmatchedCount = unmatchedTransactions.Count - matchedCount
        };
    }

    #endregion

    #region 人工匹配

    public async Task<bool> ManualMatchAsync(BankTransactionManualMatchRequest request, string? operatorName)
    {
        var transaction = await _transactionRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.FID == request.TransactionId);

        if (transaction == null) return false;

        // 匹配互斥检查：已匹配的流水不能再匹配
        if (transaction.FMatchStatus == 1)
        {
            throw new InvalidOperationException("该流水已匹配，不能重复匹配");
        }

        transaction.FMatchStatus = 1;
        transaction.FRelatedBusinessType = request.BusinessType;
        transaction.FRelatedBusinessId = request.BusinessId;
        transaction.FUpdaterName = operatorName;
        transaction.FUpdatedTime = DateTime.Now;

        await _transactionRepository.UpdateAsync(transaction);
        return true;
    }

    #endregion

    #region 无需匹配标记

    public async Task<int> SkipMatchAsync(BankTransactionSkipMatchRequest request, string? operatorName)
    {
        var count = 0;
        foreach (var id in request.TransactionIds)
        {
            var transaction = await _transactionRepository.Query()
                .AsTracking()
                .FirstOrDefaultAsync(t => t.FID == id);

            if (transaction == null) continue;

            // 只有未匹配的流水才能标记为无需匹配
            if (transaction.FMatchStatus != 0) continue;

            transaction.FMatchStatus = 2;
            transaction.FUpdaterName = operatorName;
            transaction.FUpdatedTime = DateTime.Now;

            await _transactionRepository.UpdateAsync(transaction);
            count++;
        }

        return count;
    }

    #endregion

    #region Mapping

    private static BankChannelDto MapToChannelDto(FinPaymentChannel entity)
    {
        return new BankChannelDto
        {
            Id = entity.FID,
            Name = entity.FName,
            Type = entity.FType,
            AccountNo = entity.FAccountNo,
            BankName = entity.FBankName,
            ImportTemplate = entity.FImportTemplate,
            Status = entity.FStatus,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime,
            UpdaterName = entity.FUpdaterName,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static BankTransactionDto MapToTransactionDto(FinBankTransaction entity, string? channelName)
    {
        return new BankTransactionDto
        {
            Id = entity.FID,
            ChannelId = entity.FChannelId,
            ChannelName = channelName,
            TransactionDate = entity.FTransactionDate,
            TransactionNo = entity.FTransactionNo,
            CounterpartAccount = entity.FCounterpartAccount,
            CounterpartName = entity.FCounterpartName,
            Direction = entity.FDirection,
            Amount = entity.FAmount,
            Balance = entity.FBalance,
            Summary = entity.FSummary,
            Remark = entity.FRemark,
            ImportBatchId = entity.FImportBatchId,
            MatchStatus = entity.FMatchStatus,
            RelatedBusinessType = entity.FRelatedBusinessType,
            RelatedBusinessId = entity.FRelatedBusinessId,
            VoucherId = entity.FVoucherId,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime
        };
    }

    #endregion
}
