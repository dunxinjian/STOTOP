using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class BudgetOccupationService : IBudgetOccupationService
{
    private const string DefaultPolicy = "warn";

    private readonly IRepository<FinBudgetVersion> _versionRepository;
    private readonly IRepository<FinBudgetLine> _lineRepository;
    private readonly IRepository<FinBudgetOccupation> _occupationRepository;
    private readonly IBudgetExpenseMappingService _mappingService;

    public BudgetOccupationService(
        IRepository<FinBudgetVersion> versionRepository,
        IRepository<FinBudgetLine> lineRepository,
        IRepository<FinBudgetOccupation> occupationRepository,
        IBudgetExpenseMappingService mappingService)
    {
        _versionRepository = versionRepository;
        _lineRepository = lineRepository;
        _occupationRepository = occupationRepository;
        _mappingService = mappingService;
    }

    public async Task<BudgetPreviewResult> PreviewAsync(BudgetPreviewRequest request)
    {
        var context = await BuildPreviewContextAsync(request);
        return context.Result;
    }

    public async Task OccupyAsync(BudgetPreviewRequest request, string transitionKey)
    {
        NormalizeTransitionKey(transitionKey);
        NormalizeSource(request);

        if (!request.SourceId.HasValue || request.SourceId.Value <= 0)
        {
            throw new InvalidOperationException("预算占用来源ID不能为空");
        }

        if (await HasTransitionAsync(request.SourceType, request.SourceId.Value, transitionKey))
        {
            return;
        }

        var context = await BuildPreviewContextAsync(request);
        if (context.Result.Blocked)
        {
            throw new InvalidOperationException("本次申请超过可用预算，已被预算策略阻止");
        }

        if (context.Result.MappingMissing || !context.BudgetVersionId.HasValue)
        {
            return;
        }

        var entity = await _occupationRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(o =>
                o.FSourceType == request.SourceType &&
                o.FSourceId == request.SourceId.Value &&
                o.FBudgetVersionId == context.BudgetVersionId.Value &&
                o.FPeriod == request.Period &&
                o.FStatus != "released" &&
                o.FStatus != "consumed");

        var now = DateTime.Now;
        if (entity == null)
        {
            entity = new FinBudgetOccupation
            {
                FBudgetVersionId = context.BudgetVersionId.Value,
                FBudgetLineId = context.BudgetLineId,
                FSourceType = request.SourceType,
                FSourceId = request.SourceId.Value,
                FCreatedTime = now
            };
            ApplyOccupation(entity, request, context, "occupied", transitionKey, request.Amount, now);
            await _occupationRepository.AddAsync(entity);
        }
        else
        {
            ApplyOccupation(entity, request, context, "occupied", transitionKey, request.Amount, now);
            await _occupationRepository.UpdateAsync(entity);
        }
    }

    public async Task LockAsync(string sourceType, long sourceId, string transitionKey)
    {
        await ChangeActiveStatusAsync(sourceType, sourceId, transitionKey, "locked", null);
    }

    public async Task ConsumeAsync(string sourceType, long sourceId, decimal amount, string transitionKey)
    {
        if (amount < 0)
        {
            throw new InvalidOperationException("预算消耗金额不能为负数");
        }

        await ChangeActiveStatusAsync(sourceType, sourceId, transitionKey, "consumed", amount);
    }

    public async Task ReleaseAsync(string sourceType, long sourceId, string transitionKey)
    {
        await ChangeActiveStatusAsync(sourceType, sourceId, transitionKey, "released", null);
    }

    private async Task ChangeActiveStatusAsync(string sourceType, long sourceId, string transitionKey, string status, decimal? consumedAmount)
    {
        sourceType = NormalizeSourceType(sourceType);
        NormalizeSourceId(sourceId);
        NormalizeTransitionKey(transitionKey);

        if (await HasTransitionAsync(sourceType, sourceId, transitionKey))
        {
            return;
        }

        var occupations = await _occupationRepository.Query()
            .AsTracking()
            .Where(o =>
                o.FSourceType == sourceType &&
                o.FSourceId == sourceId &&
                (o.FStatus == "occupied" || o.FStatus == "locked"))
            .OrderBy(o => o.FID)
            .ToListAsync();

        if (occupations.Count == 0)
        {
            return;
        }

        var remainingConsumedAmount = consumedAmount;
        var now = DateTime.Now;
        foreach (var occupation in occupations)
        {
            if (remainingConsumedAmount.HasValue)
            {
                var amountForLine = remainingConsumedAmount.Value <= 0
                    ? 0m
                    : Math.Min(occupation.FAmount, remainingConsumedAmount.Value);
                occupation.FAmount = amountForLine;
                remainingConsumedAmount -= amountForLine;
            }

            occupation.FStatus = status;
            occupation.FTransitionKey = transitionKey;
            occupation.FUpdatedTime = now;
            await _occupationRepository.UpdateAsync(occupation);
        }
    }

    private async Task<BudgetPreviewContext> BuildPreviewContextAsync(BudgetPreviewRequest request)
    {
        NormalizePreviewRequest(request);

        var result = new BudgetPreviewResult
        {
            AccountCode = request.AccountCode,
            PLItemId = request.PLItemId,
            RequestAmount = request.Amount,
            Policy = DefaultPolicy
        };

        if (request.AccountSetId <= 0)
        {
            result.MappingMissing = true;
            result.MissingReason = "账套ID不能为空，无法进行预算控制";
            result.GapAmount = request.Amount;
            return new BudgetPreviewContext(result);
        }

        if (request.OrgId <= 0)
        {
            result.MappingMissing = true;
            result.MissingReason = "组织不能为空，无法进行预算控制";
            result.GapAmount = request.Amount;
            return new BudgetPreviewContext(result);
        }

        if (string.IsNullOrWhiteSpace(request.AccountCode) && !request.PLItemId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(request.ExpenseType))
            {
                result.MappingMissing = true;
                result.MissingReason = "未提供费用类型，无法解析预算控制科目";
                result.GapAmount = request.Amount;
                return new BudgetPreviewContext(result);
            }

            var mapping = await _mappingService.ResolveAsync(request.AccountSetId, request.OrgId, request.ExpenseType);
            if (mapping == null)
            {
                result.MappingMissing = true;
                result.MissingReason = $"费用类型 {request.ExpenseType} 未配置预算映射";
                result.GapAmount = request.Amount;
                return new BudgetPreviewContext(result);
            }

            request.AccountCode = mapping.AccountCode;
            request.PLItemId = mapping.PLItemId;
            result.AccountCode = request.AccountCode;
            result.PLItemId = request.PLItemId;
        }

        if (string.IsNullOrWhiteSpace(request.AccountCode) && !request.PLItemId.HasValue)
        {
            result.MappingMissing = true;
            result.MissingReason = "预算映射未配置科目或损益项";
            result.GapAmount = request.Amount;
            return new BudgetPreviewContext(result);
        }

        var year = int.Parse(request.Period.Substring(0, 4));
        var budgetVersion = await _versionRepository.Query()
            .Where(v =>
                v.FAccountSetId == request.AccountSetId &&
                v.FYear == year &&
                (v.FStatus == "approved" || v.FStatus == "locked"))
            .OrderByDescending(v => v.FStatus == "locked")
            .ThenByDescending(v => v.FApprovedTime ?? v.FUpdatedTime)
            .FirstOrDefaultAsync();

        if (budgetVersion == null)
        {
            result.MissingReason = "未找到已审批预算版本";
            result.GapAmount = request.Amount;
            return new BudgetPreviewContext(result);
        }

        var budgetLines = await GetTargetBudgetLinesAsync(budgetVersion.FID, request.Period, request.OrgId, request.AccountCode, request.PLItemId);
        result.BudgetAmount = budgetLines.Sum(l => l.FAmount);

        var occupiedQuery = _occupationRepository.Query()
            .Where(o =>
                o.FBudgetVersionId == budgetVersion.FID &&
                o.FOrgId == request.OrgId &&
                o.FPeriod == request.Period &&
                (o.FStatus == "occupied" || o.FStatus == "locked"));

        if (request.SourceId.HasValue && request.SourceId.Value > 0)
        {
            occupiedQuery = occupiedQuery.Where(o => !(o.FSourceType == request.SourceType && o.FSourceId == request.SourceId.Value));
        }

        occupiedQuery = ApplyTargetFilter(occupiedQuery, request.AccountCode, request.PLItemId);
        result.OccupiedAmount = await occupiedQuery.SumAsync(o => o.FAmount);
        result.AvailableAmount = result.BudgetAmount - result.OccupiedAmount;
        result.GapAmount = Math.Max(0m, result.RequestAmount - result.AvailableAmount);
        result.Blocked = result.Policy == "block" && result.GapAmount > 0;

        return new BudgetPreviewContext(
            result,
            budgetVersion.FID,
            budgetLines.FirstOrDefault()?.FID);
    }

    private async Task<List<FinBudgetLine>> GetTargetBudgetLinesAsync(
        long budgetVersionId,
        string period,
        long orgId,
        string? accountCode,
        long? plItemId)
    {
        var query = _lineRepository.Query()
            .Where(l => l.FBudgetVersionId == budgetVersionId && l.FPeriod == period && l.FOrgId == orgId);

        query = ApplyTargetFilter(query, accountCode, plItemId);
        return await query.ToListAsync();
    }

    private static IQueryable<FinBudgetLine> ApplyTargetFilter(IQueryable<FinBudgetLine> query, string? accountCode, long? plItemId)
    {
        var hasAccountCode = !string.IsNullOrWhiteSpace(accountCode);
        var hasPLItem = plItemId.HasValue && plItemId.Value > 0;
        var plId = plItemId.GetValueOrDefault();

        if (hasAccountCode && hasPLItem)
        {
            return query.Where(l => l.FAccountCode == accountCode || l.FPLItemId == plId);
        }

        if (hasAccountCode)
        {
            return query.Where(l => l.FAccountCode == accountCode);
        }

        return query.Where(l => l.FPLItemId == plId);
    }

    private static IQueryable<FinBudgetOccupation> ApplyTargetFilter(IQueryable<FinBudgetOccupation> query, string? accountCode, long? plItemId)
    {
        var hasAccountCode = !string.IsNullOrWhiteSpace(accountCode);
        var hasPLItem = plItemId.HasValue && plItemId.Value > 0;
        var plId = plItemId.GetValueOrDefault();

        if (hasAccountCode && hasPLItem)
        {
            return query.Where(o => o.FAccountCode == accountCode || o.FPLItemId == plId);
        }

        if (hasAccountCode)
        {
            return query.Where(o => o.FAccountCode == accountCode);
        }

        return query.Where(o => o.FPLItemId == plId);
    }

    private async Task<bool> HasTransitionAsync(string sourceType, long sourceId, string transitionKey)
    {
        return await _occupationRepository.Query()
            .AnyAsync(o =>
                o.FSourceType == sourceType &&
                o.FSourceId == sourceId &&
                o.FTransitionKey == transitionKey);
    }

    private static void NormalizePreviewRequest(BudgetPreviewRequest request)
    {
        request.SourceType = NormalizeSourceType(request.SourceType);
        request.Period = NormalizePeriod(request.Period);
        request.AccountCode = NormalizeNullable(request.AccountCode);
        request.ExpenseType = NormalizeNullable(request.ExpenseType);

        if (request.Amount < 0)
        {
            throw new InvalidOperationException("预算控制金额不能为负数");
        }
    }

    private static void NormalizeSource(BudgetPreviewRequest request)
    {
        request.SourceType = NormalizeSourceType(request.SourceType);
    }

    private static string NormalizeSourceType(string sourceType)
    {
        return string.IsNullOrWhiteSpace(sourceType) ? "cardflow_card" : sourceType.Trim();
    }

    private static void NormalizeSourceId(long sourceId)
    {
        if (sourceId <= 0)
        {
            throw new InvalidOperationException("预算占用来源ID不能为空");
        }
    }

    private static void NormalizeTransitionKey(string transitionKey)
    {
        if (string.IsNullOrWhiteSpace(transitionKey))
        {
            throw new InvalidOperationException("预算占用转换键不能为空");
        }
    }

    private static string NormalizePeriod(string period)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            return DateTime.Today.ToString("yyyyMM");
        }

        var value = period.Trim();
        if (value.Length != 6 || !value.All(char.IsDigit))
        {
            throw new InvalidOperationException("预算期间格式应为YYYYMM");
        }

        var month = int.Parse(value.Substring(4, 2));
        if (month < 1 || month > 12)
        {
            throw new InvalidOperationException("预算期间月份必须在01到12之间");
        }

        return value;
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ApplyOccupation(
        FinBudgetOccupation entity,
        BudgetPreviewRequest request,
        BudgetPreviewContext context,
        string status,
        string transitionKey,
        decimal amount,
        DateTime now)
    {
        entity.FBudgetVersionId = context.BudgetVersionId!.Value;
        entity.FBudgetLineId = context.BudgetLineId;
        entity.FOrgId = request.OrgId;
        entity.FPeriod = request.Period;
        entity.FAccountCode = request.AccountCode;
        entity.FPLItemId = request.PLItemId;
        entity.FAmount = amount;
        entity.FStatus = status;
        entity.FTransitionKey = transitionKey;
        entity.FUpdatedTime = now;
    }

    private sealed class BudgetPreviewContext
    {
        public BudgetPreviewContext(
            BudgetPreviewResult result,
            long? budgetVersionId = null,
            long? budgetLineId = null)
        {
            Result = result;
            BudgetVersionId = budgetVersionId;
            BudgetLineId = budgetLineId;
        }

        public BudgetPreviewResult Result { get; }
        public long? BudgetVersionId { get; }
        public long? BudgetLineId { get; }
    }
}
