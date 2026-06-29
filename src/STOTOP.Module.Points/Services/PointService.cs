using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Points.Constants;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Entities;
using STOTOP.Module.Points.Events;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Points.Services;

public class PointService : IPointService
{
    private readonly STOTOPDbContext _db;
    private readonly IPointRuleService _ruleService;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<PointService> _logger;

    public PointService(STOTOPDbContext db, IPointRuleService ruleService, IEventDispatcher eventDispatcher, ILogger<PointService> logger)
    {
        _db = db;
        _ruleService = ruleService;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    public async Task<ApiResult<PointRecordListDto>> AwardAsync(long orgId, long operatorId, ManualAwardRequest request, int accountType = PointAccountTypes.B)
    {
        if (request.PointValue <= 0)
            return ApiResult<PointRecordListDto>.Fail("奖分积分值必须大于0");

        EnsureValidAccountType(accountType);

        // 事件级幂等查重：同一 (组织 + 事件类型 + 事件ID + 账户类型) 已写过则跳过
        if (!string.IsNullOrEmpty(request.RelatedEventType) && !string.IsNullOrEmpty(request.RelatedEventId))
        {
            var existed = await _db.Set<PmPointRecord>()
                .FirstOrDefaultAsync(r => r.FOrgId == orgId
                    && r.F关联事件类型 == request.RelatedEventType
                    && r.F关联事件ID == request.RelatedEventId
                    && r.F账户类型 == accountType);
            if (existed != null)
            {
                _logger.LogInformation(
                    "AwardAsync 命中事件幂等键，跳过：OrgId={OrgId}, EventType={EventType}, EventId={EventId}, AccountType={AccountType}",
                    orgId, request.RelatedEventType, request.RelatedEventId, accountType);
                return ApiResult<PointRecordListDto>.Success(await MapRecordToDto(existed), "幂等跳过");
            }
        }

        var record = await CreateRecordAndUpdateAccountAsync(
            orgId, request.UserId, request.SourceId, null,
            1, // Type=1 奖分
            request.PointValue, operatorId,
            request.RelatedModule ?? "Points", request.RelatedEntityType, request.RelatedEntityId,
            accountType, request.RelatedEventType, request.RelatedEventId,
            request.Remark);

        if (record == null)
            return ApiResult<PointRecordListDto>.Fail("创建积分记录失败");

        // 发布积分获得事件（供下游排行榜/KSF/PPV 等联动消费）
        try
        {
            await _eventDispatcher.PublishAsync(new PointEarnedEvent
            {
                OrgId = orgId,
                UserId = request.UserId,
                AccountType = accountType,
                Amount = request.PointValue,
                RelatedEventType = request.RelatedEventType,
                RelatedEventId = request.RelatedEventId,
                RuleId = null,
                TriggeredByUserId = operatorId,
                ModuleCode = "points"
            });
        }
        catch { /* 事件发布失败不影响主业务 */ }

        return ApiResult<PointRecordListDto>.Success(await MapRecordToDto(record));
    }

    public async Task<ApiResult<PointRecordListDto>> DeductAsync(long orgId, long operatorId, ManualDeductRequest request, int accountType = PointAccountTypes.B)
    {
        if (request.PointValue <= 0)
            return ApiResult<PointRecordListDto>.Fail("扣分积分值必须大于0");

        EnsureValidAccountType(accountType);
        EnsureConsumeFromBOnly(accountType, "扣分");

        // 事件级幂等查重
        if (!string.IsNullOrEmpty(request.RelatedEventType) && !string.IsNullOrEmpty(request.RelatedEventId))
        {
            var existed = await _db.Set<PmPointRecord>()
                .FirstOrDefaultAsync(r => r.FOrgId == orgId
                    && r.F关联事件类型 == request.RelatedEventType
                    && r.F关联事件ID == request.RelatedEventId
                    && r.F账户类型 == accountType);
            if (existed != null)
            {
                _logger.LogInformation(
                    "DeductAsync 命中事件幂等键，跳过：OrgId={OrgId}, EventType={EventType}, EventId={EventId}, AccountType={AccountType}",
                    orgId, request.RelatedEventType, request.RelatedEventId, accountType);
                return ApiResult<PointRecordListDto>.Success(await MapRecordToDto(existed), "幂等跳过");
            }
        }

        var record = await CreateRecordAndUpdateAccountAsync(
            orgId, request.UserId, request.SourceId, null,
            2, // Type=2 扣分
            -request.PointValue, operatorId,
            request.RelatedModule ?? "Points", request.RelatedEntityType, request.RelatedEntityId,
            accountType, request.RelatedEventType, request.RelatedEventId,
            request.Remark);

        if (record == null)
            return ApiResult<PointRecordListDto>.Fail("创建积分记录失败");

        // 发布积分扣减事件（Amount 为绝对值）
        try
        {
            await _eventDispatcher.PublishAsync(new PointDeductedEvent
            {
                OrgId = orgId,
                UserId = request.UserId,
                AccountType = accountType,
                Amount = request.PointValue,
                RelatedEventType = request.RelatedEventType,
                RelatedEventId = request.RelatedEventId,
                RuleId = null,
                Reason = request.Remark,
                TriggeredByUserId = operatorId,
                ModuleCode = "points"
            });
        }
        catch { /* 事件发布失败不影响主业务 */ }

        return ApiResult<PointRecordListDto>.Success(await MapRecordToDto(record));
    }

    public async Task<ApiResult<bool>> TriggerEventAsync(PointEventDto eventDto)
    {
        // 1. 匹配规则（同一事件类型可命中多条规则，分别路由 A/B 账户）
        var matchedRules = await _ruleService.MatchRulesAsync(eventDto.OrgId, eventDto.EventType, eventDto.Context);
        if (matchedRules.Count == 0)
            return ApiResult<bool>.Success(true, "未匹配到规则");

        // 2. 查找来源（通过 SourceModule 匹配）
        var source = await _db.Set<PmPointSource>()
            .FirstOrDefaultAsync(s => s.FOrgId == eventDto.OrgId && s.FSourceCode == eventDto.SourceModule && s.FIsEnabled);
        long sourceId = source?.FID ?? 0;

        // 事件级幂等键所需字段
        var relatedEventType = eventDto.EventType;
        var relatedEventId = eventDto.EntityId.HasValue ? eventDto.EntityId.Value.ToString() : null;

        foreach (var rule in matchedRules)
        {
            var accountType = rule.AccountType <= 0 ? PointAccountTypes.B : rule.AccountType;

            // 3. 事件级幂等查重：同一 (组织 + 事件类型 + 事件ID + 账户类型) 已写过则跳过
            if (!string.IsNullOrEmpty(relatedEventId))
            {
                var duplicated = await _db.Set<PmPointRecord>()
                    .AnyAsync(r => r.FOrgId == eventDto.OrgId
                        && r.F关联事件类型 == relatedEventType
                        && r.F关联事件ID == relatedEventId
                        && r.F账户类型 == accountType);
                if (duplicated)
                {
                    _logger.LogInformation(
                        "TriggerEventAsync 命中事件幂等键，跳过：OrgId={OrgId}, EventType={EventType}, EventId={EventId}, AccountType={AccountType}",
                        eventDto.OrgId, relatedEventType, relatedEventId, accountType);
                    continue;
                }
            }

            // 4. 检查周期上限
            if (rule.CycleLimit > 0)
            {
                var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var cycleCount = await _db.Set<PmPointRecord>()
                    .CountAsync(r => r.FOrgId == eventDto.OrgId
                        && r.FUserId == eventDto.UserId
                        && r.FRuleId == rule.Id
                        && r.FCreateTime >= monthStart);

                if (cycleCount >= rule.CycleLimit)
                    continue;
            }

            // 5. 计算积分值（MatchRulesAsync 已处理倍率，再叠加规则配置的转换比例）
            int pointValue = (int)Math.Round(rule.PointValue * rule.ConvertRatio);

            // 6. 判断是否需要审批
            if (rule.RequireApproval)
            {
                var application = new PmPointApplication
                {
                    FOrgId = eventDto.OrgId,
                    FApplicantId = eventDto.UserId,
                    FRuleId = rule.Id,
                    FApplicationNote = $"自动触发：{eventDto.EventType}",
                    FStatus = 0,
                    F账户类型 = accountType,
                    FCreateTime = DateTime.Now
                };
                _db.Set<PmPointApplication>().Add(application);
                await _db.SaveChangesAsync();
            }
            else
            {
                int type = pointValue >= 0 ? 1 : 2;
                await CreateRecordAndUpdateAccountAsync(
                    eventDto.OrgId, eventDto.UserId,
                    sourceId > 0 ? sourceId : rule.SourceId,
                    rule.Id, type, pointValue, eventDto.UserId,
                    eventDto.SourceModule, eventDto.EntityType, eventDto.EntityId,
                    accountType, relatedEventType, relatedEventId,
                    $"事件触发：{eventDto.EventType}");
            }
        }

        return ApiResult<bool>.Success(true, "事件处理完成");
    }

    public async Task<ApiResult<PagedResult<PointRecordListDto>>> GetRecordsPagedAsync(long orgId, PointRecordPagedRequest request)
    {
        var query = _db.Set<PmPointRecord>().Where(r => r.FOrgId == orgId).AsQueryable();
        query = ApplyRecordFilters(query, request);
        var total = await query.CountAsync();
        var records = await query
            .OrderByDescending(r => r.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();
        var items = await MapRecordsToDtos(records);
        return ApiResult<PagedResult<PointRecordListDto>>.Success(new PagedResult<PointRecordListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<PagedResult<PointRecordListDto>>> GetMyRecordsAsync(long orgId, long userId, PointRecordPagedRequest request)
    {
        var query = _db.Set<PmPointRecord>().Where(r => r.FOrgId == orgId && r.FUserId == userId).AsQueryable();
        query = ApplyRecordFilters(query, request);
        var total = await query.CountAsync();
        var records = await query
            .OrderByDescending(r => r.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();
        var items = await MapRecordsToDtos(records);
        return ApiResult<PagedResult<PointRecordListDto>>.Success(new PagedResult<PointRecordListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<PointAccountDto>> GetAccountAsync(long orgId, long userId)
    {
        var accounts = await _db.Set<PmPointAccount>()
            .Where(a => a.FOrgId == orgId && a.FUserId == userId)
            .ToListAsync();

        if (accounts.Count == 0)
            return ApiResult<PointAccountDto>.Fail("积分账户不存在");

        var dto = AggregateAccounts(orgId, userId, accounts);
        var user = await _db.Set<SysUser>()
            .Where(u => u.FID == userId)
            .Select(u => new { u.FName })
            .FirstOrDefaultAsync();
        dto.UserName = user?.FName;
        return ApiResult<PointAccountDto>.Success(dto);
    }

    public async Task<ApiResult<PointAccountDto>> GetAccountByTypeAsync(long orgId, long userId, int accountType)
    {
        EnsureValidAccountType(accountType);

        var account = await _db.Set<PmPointAccount>()
            .FirstOrDefaultAsync(a => a.FOrgId == orgId && a.FUserId == userId && a.F账户类型 == accountType);

        if (account == null)
            return ApiResult<PointAccountDto>.Fail($"账户类型={accountType} 的积分账户不存在");

        var dto = MapAccountToDto(account);
        var user = await _db.Set<SysUser>()
            .Where(u => u.FID == userId)
            .Select(u => new { u.FName })
            .FirstOrDefaultAsync();
        dto.UserName = user?.FName;
        return ApiResult<PointAccountDto>.Success(dto);
    }

    /// <summary>
    /// 计算账户在指定日期的余额。半开区间策略：
    ///   atDate >= 快照日期： snapshotValue + Σ流水(F创建时间 IN (snapshotDate, atDate])
    ///   atDate <  快照日期： 回退全流水累计 + 警告日志
    ///   无快照：              全流水累计（截止 atDate）
    /// </summary>
    public async Task<ApiResult<int>> GetAccountBalanceAtDateAsync(long orgId, long userId, int accountType, DateTime atDate)
    {
        EnsureValidAccountType(accountType);

        var account = await _db.Set<PmPointAccount>()
            .FirstOrDefaultAsync(a => a.FOrgId == orgId && a.FUserId == userId && a.F账户类型 == accountType);

        if (account == null)
            return ApiResult<int>.Fail($"账户类型={accountType} 的积分账户不存在");

        var snapshotDate = account.F期初余额快照日期;
        var snapshotValue = account.F期初余额快照值;

        if (snapshotDate.HasValue && atDate >= snapshotDate.Value)
        {
            // 半开区间 (snapshotDate, atDate]：使用快照值 + 此后的流水增量
            var delta = await _db.Set<PmPointRecord>()
                .Where(r => r.FOrgId == orgId
                    && r.FUserId == userId
                    && r.F账户类型 == accountType
                    && r.FCreateTime > snapshotDate.Value
                    && r.FCreateTime <= atDate)
                .SumAsync(r => (int?)r.FPointValue) ?? 0;
            return ApiResult<int>.Success(snapshotValue + delta);
        }

        if (snapshotDate.HasValue && atDate < snapshotDate.Value)
        {
            _logger.LogWarning(
                "GetAccountBalanceAtDateAsync 回退至全流水累计：OrgId={OrgId}, UserId={UserId}, AccountType={AccountType}, AtDate={AtDate}, SnapshotDate={SnapshotDate}",
                orgId, userId, accountType, atDate, snapshotDate);
        }

        var fullSum = await _db.Set<PmPointRecord>()
            .Where(r => r.FOrgId == orgId
                && r.FUserId == userId
                && r.F账户类型 == accountType
                && r.FCreateTime <= atDate)
            .SumAsync(r => (int?)r.FPointValue) ?? 0;
        return ApiResult<int>.Success(fullSum);
    }

    public async Task<ApiResult<PointAccountDto>> GetMyAccountAsync(long orgId, long userId)
    {
        var accounts = await _db.Set<PmPointAccount>()
            .Where(a => a.FOrgId == orgId && a.FUserId == userId)
            .ToListAsync();

        // 自动补齐缺失的 A/B 账户
        if (!accounts.Any(a => a.F账户类型 == PointAccountTypes.A))
        {
            accounts.Add(await EnsureAccountAsync(orgId, userId, PointAccountTypes.A));
        }
        if (!accounts.Any(a => a.F账户类型 == PointAccountTypes.B))
        {
            accounts.Add(await EnsureAccountAsync(orgId, userId, PointAccountTypes.B));
        }

        var dto = AggregateAccounts(orgId, userId, accounts);
        var user = await _db.Set<SysUser>()
            .Where(u => u.FID == userId)
            .Select(u => new { u.FName })
            .FirstOrDefaultAsync();
        dto.UserName = user?.FName;
        return ApiResult<PointAccountDto>.Success(dto);
    }

    public async Task<ApiResult<PointStatisticsDto>> GetStatisticsAsync(long orgId, long userId)
    {
        var accounts = await _db.Set<PmPointAccount>()
            .Where(a => a.FOrgId == orgId && a.FUserId == userId)
            .ToListAsync();

        var stats = new PointStatisticsDto
        {
            TotalPoints = accounts.Sum(a => a.FTotalPoints),
            AvailablePoints = accounts.Sum(a => a.FAvailablePoints),
            MonthlyAward = accounts.Sum(a => a.FMonthlyAward),
            MonthlyDeduct = accounts.Sum(a => a.FMonthlyDeduct),
            YearlyPoints = accounts.Sum(a => a.FYearlyPoints)
        };

        var currentPeriod = DateTime.Now.ToString("yyyy-MM");
        var ranking = await _db.Set<PmPointRanking>()
            .FirstOrDefaultAsync(r => r.FOrgId == orgId && r.FUserId == userId
                && r.FDimension == 0 && r.FPeriod == currentPeriod);
        stats.CurrentRank = ranking?.FRank;

        var sourceStats = await _db.Set<PmPointRecord>()
            .Where(r => r.FOrgId == orgId && r.FUserId == userId)
            .GroupBy(r => r.FSourceId)
            .Select(g => new { SourceId = g.Key, Total = g.Sum(r => r.FPointValue), Count = g.Count() })
            .ToListAsync();

        var sourceIds = sourceStats.Select(s => s.SourceId).ToList();
        var sources = await _db.Set<PmPointSource>()
            .Where(s => sourceIds.Contains(s.FID))
            .Select(s => new { s.FID, s.FSourceName, s.FColor })
            .ToListAsync();
        var sourceDict = sources.ToDictionary(s => s.FID);

        stats.BySource = sourceStats.Select(s => new SourceStatItem
        {
            SourceId = s.SourceId,
            SourceName = sourceDict.GetValueOrDefault(s.SourceId)?.FSourceName ?? "",
            Color = sourceDict.GetValueOrDefault(s.SourceId)?.FColor,
            TotalPoints = s.Total,
            Count = s.Count
        }).ToList();

        var sixMonthsAgo = DateTime.Now.AddMonths(-5);
        var monthStart = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);

        var monthlyRecords = await _db.Set<PmPointRecord>()
            .Where(r => r.FOrgId == orgId && r.FUserId == userId && r.FCreateTime >= monthStart)
            .ToListAsync();

        var monthlyGroups = monthlyRecords
            .GroupBy(r => r.FCreateTime.ToString("yyyy-MM"))
            .OrderBy(g => g.Key);

        stats.MonthlyTrend = monthlyGroups.Select(g => new TrendItem
        {
            Period = g.Key,
            AwardPoints = g.Where(r => r.FType == 1).Sum(r => r.FPointValue),
            DeductPoints = g.Where(r => r.FType == 2).Sum(r => Math.Abs(r.FPointValue)),
            NetPoints = g.Sum(r => r.FPointValue)
        }).ToList();

        return ApiResult<PointStatisticsDto>.Success(stats);
    }

    /// <summary>
    /// 创建积分记录并更新账户（事务，按 accountType 路由到对应账户行）
    /// </summary>
    internal async Task<PmPointRecord?> CreateRecordAndUpdateAccountAsync(
        long orgId, long userId, long sourceId, long? ruleId,
        int type, int pointValue, long operatorId,
        string? relatedModule, string? relatedEntityType, long? relatedEntityId,
        int accountType,
        string? relatedEventType, string? relatedEventId,
        string remark)
    {
        EnsureValidAccountType(accountType);
        if (pointValue < 0)
            EnsureConsumeFromBOnly(accountType, "扣减");

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var account = await _db.Set<PmPointAccount>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.FOrgId == orgId && a.FUserId == userId && a.F账户类型 == accountType);

            if (account == null)
            {
                account = await EnsureAccountAsync(orgId, userId, accountType);
            }

            // 原子更新账户积分，防止并发奖/扣分的读-改-写丢失更新（pointValue 扣分时为负数）
            if (type == 1) // 奖分
            {
                await _db.Set<PmPointAccount>()
                    .Where(a => a.FID == account.FID)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(a => a.FTotalPoints, a => a.FTotalPoints + pointValue)
                        .SetProperty(a => a.FAvailablePoints, a => a.FAvailablePoints + pointValue)
                        .SetProperty(a => a.FMonthlyAward, a => a.FMonthlyAward + pointValue)
                        .SetProperty(a => a.FYearlyPoints, a => a.FYearlyPoints + pointValue)
                        .SetProperty(a => a.FUpdateTime, DateTime.Now));
            }
            else // 扣分（pointValue 为负数）
            {
                await _db.Set<PmPointAccount>()
                    .Where(a => a.FID == account.FID)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(a => a.FTotalPoints, a => a.FTotalPoints + pointValue)
                        .SetProperty(a => a.FAvailablePoints, a => a.FAvailablePoints + pointValue)
                        .SetProperty(a => a.FMonthlyDeduct, a => a.FMonthlyDeduct - pointValue)
                        .SetProperty(a => a.FYearlyPoints, a => a.FYearlyPoints + pointValue)
                        .SetProperty(a => a.FUpdateTime, DateTime.Now));
            }

            // 重读扣减后余额用于记录快照
            var newAvailable = await _db.Set<PmPointAccount>().AsNoTracking()
                .Where(a => a.FID == account.FID)
                .Select(a => a.FAvailablePoints)
                .FirstOrDefaultAsync();

            var record = new PmPointRecord
            {
                FOrgId = orgId,
                FUserId = userId,
                FSourceId = sourceId,
                FRuleId = ruleId,
                FType = type,
                FPointValue = pointValue,
                FBalance = newAvailable,
                FRelatedModule = relatedModule,
                FRelatedEntityType = relatedEntityType,
                FRelatedEntityId = relatedEntityId,
                FOperatorId = operatorId,
                FRemark = remark,
                F账户类型 = accountType,
                F关联事件类型 = relatedEventType,
                F关联事件ID = relatedEventId,
                FCreateTime = DateTime.Now
            };

            _db.Set<PmPointRecord>().Add(record);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return record;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "CreateRecordAndUpdateAccountAsync 失败：OrgId={OrgId}, UserId={UserId}, AccountType={AccountType}", orgId, userId, accountType);
            return null;
        }
    }

    /// <summary>
    /// 确保 (orgId, userId, accountType) 账户存在，否则创建一行（不提交事务，由调用方负责）
    /// </summary>
    private async Task<PmPointAccount> EnsureAccountAsync(long orgId, long userId, int accountType)
    {
        var account = await _db.Set<PmPointAccount>()
            .AsTracking()
            .FirstOrDefaultAsync(a => a.FOrgId == orgId && a.FUserId == userId && a.F账户类型 == accountType);

        if (account != null) return account;

        account = new PmPointAccount
        {
            FOrgId = orgId,
            FUserId = userId,
            F账户类型 = accountType,
            FTotalPoints = 0,
            FUsedPoints = 0,
            FAvailablePoints = 0,
            FMonthlyAward = 0,
            FMonthlyDeduct = 0,
            FYearlyPoints = 0,
            F期初余额快照值 = 0,
            FUpdateTime = DateTime.Now
        };
        _db.Set<PmPointAccount>().Add(account);
        await _db.SaveChangesAsync();
        return account;
    }

    private static void EnsureValidAccountType(int accountType)
    {
        if (accountType != PointAccountTypes.A && accountType != PointAccountTypes.B)
            throw new InvalidOperationException($"非法的账户类型：{accountType}（合法值：1=A，2=B）");
    }

    private static void EnsureConsumeFromBOnly(int accountType, string action)
    {
        if (accountType == PointAccountTypes.A)
            throw new InvalidOperationException($"业务规则违反：{action} 操作仅允许从 B 分账户扣减，不可作用于 A 分账户");
    }

    private static IQueryable<PmPointRecord> ApplyRecordFilters(IQueryable<PmPointRecord> query, PointRecordPagedRequest request)
    {
        if (request.UserId.HasValue)
            query = query.Where(r => r.FUserId == request.UserId.Value);
        if (request.SourceId.HasValue)
            query = query.Where(r => r.FSourceId == request.SourceId.Value);
        if (request.Type.HasValue)
            query = query.Where(r => r.FType == request.Type.Value);
        if (request.AccountType.HasValue)
            query = query.Where(r => r.F账户类型 == request.AccountType.Value);
        if (!string.IsNullOrWhiteSpace(request.RelatedModule))
            query = query.Where(r => r.FRelatedModule == request.RelatedModule);
        if (!string.IsNullOrWhiteSpace(request.RelatedEntityType))
            query = query.Where(r => r.FRelatedEntityType == request.RelatedEntityType);
        if (request.StartTime.HasValue)
            query = query.Where(r => r.FCreateTime >= request.StartTime.Value);
        if (request.EndTime.HasValue)
            query = query.Where(r => r.FCreateTime <= request.EndTime.Value);
        return query;
    }

    private async Task<PointRecordListDto> MapRecordToDto(PmPointRecord r)
    {
        var dto = new PointRecordListDto
        {
            Id = r.FID,
            OrgId = r.FOrgId,
            UserId = r.FUserId,
            SourceId = r.FSourceId,
            RuleId = r.FRuleId,
            Type = r.FType,
            PointValue = r.FPointValue,
            Balance = r.FBalance,
            RelatedModule = r.FRelatedModule,
            RelatedEntityType = r.FRelatedEntityType,
            RelatedEntityId = r.FRelatedEntityId,
            OperatorId = r.FOperatorId,
            Remark = r.FRemark,
            AccountType = r.F账户类型,
            CreateTime = r.FCreateTime
        };

        var user = await _db.Set<SysUser>()
            .Where(u => u.FID == r.FUserId)
            .Select(u => new { u.FName })
            .FirstOrDefaultAsync();
        dto.UserName = user?.FName;

        if (r.FOperatorId != r.FUserId)
        {
            var op = await _db.Set<SysUser>()
                .Where(u => u.FID == r.FOperatorId)
                .Select(u => new { u.FName })
                .FirstOrDefaultAsync();
            dto.OperatorName = op?.FName;
        }
        else
        {
            dto.OperatorName = user?.FName;
        }

        var source = await _db.Set<PmPointSource>()
            .Where(s => s.FID == r.FSourceId)
            .Select(s => new { s.FSourceName })
            .FirstOrDefaultAsync();
        dto.SourceName = source?.FSourceName;

        if (r.FRuleId.HasValue)
        {
            var rule = await _db.Set<PmPointRule>()
                .Where(rl => rl.FID == r.FRuleId.Value)
                .Select(rl => new { rl.FRuleName })
                .FirstOrDefaultAsync();
            dto.RuleName = rule?.FRuleName;
        }

        return dto;
    }

    private async Task<List<PointRecordListDto>> MapRecordsToDtos(List<PmPointRecord> records)
    {
        if (records.Count == 0) return new List<PointRecordListDto>();

        var userIds = records.Select(r => r.FUserId)
            .Union(records.Select(r => r.FOperatorId))
            .Distinct().ToList();
        var users = await _db.Set<SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        var userDict = users.ToDictionary(u => u.FID, u => u.FName);

        var sourceIds = records.Select(r => r.FSourceId).Distinct().ToList();
        var sources = await _db.Set<PmPointSource>()
            .Where(s => sourceIds.Contains(s.FID))
            .Select(s => new { s.FID, s.FSourceName })
            .ToListAsync();
        var sourceDict = sources.ToDictionary(s => s.FID, s => s.FSourceName);

        var ruleIds = records.Where(r => r.FRuleId.HasValue).Select(r => r.FRuleId!.Value).Distinct().ToList();
        var rules = await _db.Set<PmPointRule>()
            .Where(rl => ruleIds.Contains(rl.FID))
            .Select(rl => new { rl.FID, rl.FRuleName })
            .ToListAsync();
        var ruleDict = rules.ToDictionary(rl => rl.FID, rl => rl.FRuleName);

        return records.Select(r => new PointRecordListDto
        {
            Id = r.FID,
            OrgId = r.FOrgId,
            UserId = r.FUserId,
            UserName = userDict.GetValueOrDefault(r.FUserId),
            SourceId = r.FSourceId,
            SourceName = sourceDict.GetValueOrDefault(r.FSourceId),
            RuleId = r.FRuleId,
            RuleName = r.FRuleId.HasValue ? ruleDict.GetValueOrDefault(r.FRuleId.Value) : null,
            Type = r.FType,
            PointValue = r.FPointValue,
            Balance = r.FBalance,
            RelatedModule = r.FRelatedModule,
            RelatedEntityType = r.FRelatedEntityType,
            RelatedEntityId = r.FRelatedEntityId,
            OperatorId = r.FOperatorId,
            OperatorName = userDict.GetValueOrDefault(r.FOperatorId),
            Remark = r.FRemark,
            AccountType = r.F账户类型,
            CreateTime = r.FCreateTime
        }).ToList();
    }

    private static PointAccountDto MapAccountToDto(PmPointAccount a) => new()
    {
        Id = a.FID,
        OrgId = a.FOrgId,
        UserId = a.FUserId,
        AccountType = a.F账户类型,
        TotalPoints = a.FTotalPoints,
        UsedPoints = a.FUsedPoints,
        AvailablePoints = a.FAvailablePoints,
        MonthlyAward = a.FMonthlyAward,
        MonthlyDeduct = a.FMonthlyDeduct,
        YearlyPoints = a.FYearlyPoints,
        SnapshotDate = a.F期初余额快照日期,
        SnapshotValue = a.F期初余额快照值,
        FAPoints = a.F账户类型 == PointAccountTypes.A ? a.FAvailablePoints : 0,
        FBPoints = a.F账户类型 == PointAccountTypes.B ? a.FAvailablePoints : 0,
        UpdateTime = a.FUpdateTime
    };

    /// <summary>
    /// 将 A/B 双账户聚合为单个 PointAccountDto，FAPoints/FBPoints 分别填充各自可用余额
    /// </summary>
    private static PointAccountDto AggregateAccounts(long orgId, long userId, List<PmPointAccount> accounts)
    {
        var aAcc = accounts.FirstOrDefault(a => a.F账户类型 == PointAccountTypes.A);
        var bAcc = accounts.FirstOrDefault(a => a.F账户类型 == PointAccountTypes.B);

        return new PointAccountDto
        {
            Id = bAcc?.FID ?? aAcc?.FID ?? 0,
            OrgId = orgId,
            UserId = userId,
            AccountType = 0, // 0 表示聚合视图
            TotalPoints = accounts.Sum(a => a.FTotalPoints),
            UsedPoints = accounts.Sum(a => a.FUsedPoints),
            AvailablePoints = accounts.Sum(a => a.FAvailablePoints),
            MonthlyAward = accounts.Sum(a => a.FMonthlyAward),
            MonthlyDeduct = accounts.Sum(a => a.FMonthlyDeduct),
            YearlyPoints = accounts.Sum(a => a.FYearlyPoints),
            SnapshotDate = bAcc?.F期初余额快照日期 ?? aAcc?.F期初余额快照日期,
            SnapshotValue = (bAcc?.F期初余额快照值 ?? 0) + (aAcc?.F期初余额快照值 ?? 0),
            FAPoints = aAcc?.FAvailablePoints ?? 0,
            FBPoints = bAcc?.FAvailablePoints ?? 0,
            UpdateTime = accounts.Max(a => a.FUpdateTime)
        };
    }
}
