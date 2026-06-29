using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Quality.Services.Dashboard;

public class QualityDashboardService : IQualityDashboardService
{
    private readonly STOTOPDbContext _db;

    public QualityDashboardService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<DashboardStatsDto>> GetStatsAsync(long orgId)
    {
        var exceptions = _db.Set<QlException>().Where(e => e.FOrgId == orgId);
        var today = DateTime.Today;
        // 本周从周一开始
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var weekStart = today.AddDays(-daysSinceMonday);

        var totalCount = await exceptions.CountAsync();
        var pendingCount = await exceptions.CountAsync(e => e.FStatus == (int)ExceptionStatus.Pending);
        var processingCount = await exceptions.CountAsync(e => e.FStatus == (int)ExceptionStatus.Processing);
        var overdueCount = await exceptions.CountAsync(e => e.FStatus == (int)ExceptionStatus.Overdue);
        var closedCount = await exceptions.CountAsync(e => e.FStatus == (int)ExceptionStatus.Closed);

        // 平均处理时长（小时）：已关闭且有关闭时间的异常，先拉到内存再计算避免LINQ翻译失败
        var closedTimes = await exceptions
            .Where(e => e.FStatus == (int)ExceptionStatus.Closed && e.FClosedTime != null)
            .Select(e => new { e.FCreateTime, ClosedTime = e.FClosedTime!.Value })
            .ToListAsync();
        var avgMinutes = closedTimes.Count > 0
            ? closedTimes.Average(e => (e.ClosedTime - e.FCreateTime).TotalMinutes)
            : 0;

        // 超时率：已超时 / (总数 - 已关闭) * 100
        var activeCount = totalCount - closedCount;
        var overdueRate = activeCount > 0 ? Math.Round((double)overdueCount / activeCount * 100, 1) : 0;

        var stats = new DashboardStatsDto
        {
            TotalExceptions = totalCount,
            PendingCount = pendingCount,
            ProcessingCount = processingCount,
            OverdueCount = overdueCount,
            ClosedCount = closedCount,
            TodayNewCount = await exceptions.CountAsync(e => e.FCreateTime >= today),
            WeekNewCount = await exceptions.CountAsync(e => e.FCreateTime >= weekStart),
            AvgResolutionHours = Math.Round(avgMinutes / 60.0, 1),
            OverdueRate = overdueRate,
            RuleCount = await _db.Set<QlRule>().CountAsync(r => r.FOrgId == orgId && r.FStatus == 1),
            KnowledgeCount = await _db.Set<QlKnowledge>().CountAsync(k => k.FOrgId == orgId),
        };

        return ApiResult<DashboardStatsDto>.Success(stats);
    }

    public async Task<ApiResult<List<ExceptionListDto>>> GetRecentExceptionsAsync(long orgId, int count)
    {
        var entities = await _db.Set<QlException>()
            .Where(e => e.FOrgId == orgId)
            .OrderByDescending(e => e.FCreateTime)
            .Take(count)
            .Select(e => new
            {
                e.FID,
                e.FTitle,
                e.FType,
                e.FStatus,
                e.FPriority,
                e.FSource,
                e.FDeadline,
                e.FCreateTime,
            })
            .ToListAsync();

        var list = entities.Select(e => new ExceptionListDto
        {
            Id = e.FID,
            Title = e.FTitle,
            Type = e.FType,
            TypeText = MapExceptionType(e.FType),
            Status = e.FStatus,
            StatusText = MapExceptionStatus(e.FStatus),
            Priority = e.FPriority,
            PriorityText = MapPriority(e.FPriority),
            Source = e.FSource,
            Deadline = e.FDeadline,
            CreateTime = e.FCreateTime,
        }).ToList();

        return ApiResult<List<ExceptionListDto>>.Success(list);
    }

    public async Task<ApiResult<List<TrendDataPoint>>> GetTrendDataAsync(long orgId, int days)
    {
        if (days <= 0) days = 30;
        var startDate = DateTime.Today.AddDays(-(days - 1));

        var dbData = await _db.Set<QlException>()
            .Where(e => e.FOrgId == orgId && e.FCreateTime >= startDate)
            .GroupBy(e => e.FCreateTime.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var lookup = dbData.ToDictionary(d => d.Date, d => d.Count);

        var result = new List<TrendDataPoint>();
        for (var date = startDate; date <= DateTime.Today; date = date.AddDays(1))
        {
            result.Add(new TrendDataPoint
            {
                Date = date.ToString("yyyy-MM-dd"),
                Count = lookup.TryGetValue(date, out var c) ? c : 0
            });
        }

        return ApiResult<List<TrendDataPoint>>.Success(result);
    }

    public async Task<ApiResult<List<DistributionItem>>> GetTypeDistributionAsync(long orgId)
    {
        var data = await _db.Set<QlException>()
            .Where(e => e.FOrgId == orgId)
            .GroupBy(e => e.FType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        var result = data
            .Select(d => new DistributionItem
            {
                Name = MapExceptionType(d.Type),
                Value = d.Count
            })
            .ToList();

        return ApiResult<List<DistributionItem>>.Success(result);
    }

    public async Task<ApiResult<List<DistributionItem>>> GetPriorityDistributionAsync(long orgId)
    {
        var data = await _db.Set<QlException>()
            .Where(e => e.FOrgId == orgId)
            .GroupBy(e => e.FPriority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToListAsync();

        var result = data
            .Select(d => new DistributionItem
            {
                Name = MapPriority(d.Priority),
                Value = d.Count
            })
            .ToList();

        return ApiResult<List<DistributionItem>>.Success(result);
    }

    public async Task<ApiResult<List<TrendAnalysisDto>>> GetTrendAnalysisAsync(long orgId, DateTime? startDate, DateTime? endDate, string groupBy)
    {
        var start = startDate ?? DateTime.Now.AddDays(-30);
        var end = endDate ?? DateTime.Now;

        var rawData = await _db.Set<QlException>()
            .Where(e => e.FOrgId == orgId && e.FCreateTime >= start && e.FCreateTime <= end)
            .Select(e => new { e.FCreateTime, e.FType })
            .ToListAsync();

        List<TrendAnalysisDto> result;
        switch (groupBy?.ToLower())
        {
            case "week":
                result = rawData
                    .GroupBy(e =>
                    {
                        var date = e.FCreateTime.Date;
                        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
                        return date.AddDays(-daysSinceMonday);
                    })
                    .OrderBy(g => g.Key)
                    .Select(g => new TrendAnalysisDto
                    {
                        Period = g.Key.ToString("yyyy-MM-dd"),
                        Total = g.Count(),
                        DataException = g.Count(e => e.FType == 0),
                        ProcessException = g.Count(e => e.FType == 1),
                        BusinessException = g.Count(e => e.FType == 2),
                    })
                    .ToList();
                break;
            case "month":
                result = rawData
                    .GroupBy(e => new { e.FCreateTime.Year, e.FCreateTime.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g => new TrendAnalysisDto
                    {
                        Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Total = g.Count(),
                        DataException = g.Count(e => e.FType == 0),
                        ProcessException = g.Count(e => e.FType == 1),
                        BusinessException = g.Count(e => e.FType == 2),
                    })
                    .ToList();
                break;
            default: // day
                result = rawData
                    .GroupBy(e => e.FCreateTime.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new TrendAnalysisDto
                    {
                        Period = g.Key.ToString("yyyy-MM-dd"),
                        Total = g.Count(),
                        DataException = g.Count(e => e.FType == 0),
                        ProcessException = g.Count(e => e.FType == 1),
                        BusinessException = g.Count(e => e.FType == 2),
                    })
                    .ToList();
                break;
        }

        return ApiResult<List<TrendAnalysisDto>>.Success(result);
    }

    public async Task<ApiResult<EfficiencyAnalysisDto>> GetEfficiencyAnalysisAsync(long orgId, DateTime? startDate, DateTime? endDate)
    {
        var start = startDate ?? DateTime.Now.AddDays(-30);
        var end = endDate ?? DateTime.Now;

        var query = _db.Set<QlException>()
            .Where(e => e.FOrgId == orgId && e.FCreateTime >= start && e.FCreateTime <= end);

        var closedQuery = query.Where(e => e.FStatus == (int)ExceptionStatus.Closed && e.FClosedTime != null);

        var totalClosed = await closedQuery.CountAsync();
        var totalOverdue = await query.CountAsync(e => e.FStatus == (int)ExceptionStatus.Overdue);

        // 先拉到内存再计算时间差，避免EF Core LINQ翻译失败
        var closedItems = await closedQuery
            .Select(e => new { e.FCreateTime, ClosedTime = e.FClosedTime!.Value, e.FType, e.FPriority })
            .ToListAsync();
        var avgMinutes = closedItems.Count > 0
            ? closedItems.Average(e => (e.ClosedTime - e.FCreateTime).TotalMinutes)
            : 0;

        var totalActive = await query.CountAsync(e => e.FStatus != (int)ExceptionStatus.Closed);
        var overdueRate = totalActive > 0 ? Math.Round((double)totalOverdue / totalActive * 100, 1) : 0;

        // 按类型分组（在内存中计算，closedItems已在前面查询）
        var byType = closedItems
            .GroupBy(e => e.FType)
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count(),
                AvgMin = g.Average(e => (e.ClosedTime - e.FCreateTime).TotalMinutes)
            })
            .ToList();

        // 按优先级分组（在内存中计算）
        var byPriority = closedItems
            .GroupBy(e => e.FPriority)
            .Select(g => new
            {
                Priority = g.Key,
                Count = g.Count(),
                AvgMin = g.Average(e => (e.ClosedTime - e.FCreateTime).TotalMinutes)
            })
            .ToList();

        var dto = new EfficiencyAnalysisDto
        {
            AvgResolutionHours = Math.Round(avgMinutes / 60.0, 1),
            TotalClosed = totalClosed,
            TotalOverdue = totalOverdue,
            OverdueRate = overdueRate,
            ByType = byType.Select(t => new TypeEfficiency
            {
                TypeName = MapExceptionType(t.Type),
                Count = t.Count,
                AvgHours = Math.Round(t.AvgMin / 60.0, 1)
            }).ToList(),
            ByPriority = byPriority.Select(p => new PriorityEfficiency
            {
                PriorityName = MapPriority(p.Priority),
                Count = p.Count,
                AvgHours = Math.Round(p.AvgMin / 60.0, 1)
            }).ToList()
        };

        return ApiResult<EfficiencyAnalysisDto>.Success(dto);
    }

    public async Task<ApiResult<List<SourceDistributionDto>>> GetSourceDistributionAsync(long orgId, DateTime? startDate, DateTime? endDate)
    {
        var start = startDate ?? DateTime.Now.AddDays(-30);
        var end = endDate ?? DateTime.Now;

        var data = await _db.Set<QlException>()
            .Where(e => e.FOrgId == orgId && e.FCreateTime >= start && e.FCreateTime <= end)
            .GroupBy(e => e.FSource ?? "")
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToListAsync();

        var total = data.Sum(d => d.Count);

        var result = data
            .OrderByDescending(d => d.Count)
            .Select(d => new SourceDistributionDto
            {
                Source = string.IsNullOrEmpty(d.Source) ? "未指定" : d.Source,
                Count = d.Count,
                Percentage = total > 0 ? Math.Round((double)d.Count / total * 100, 1) : 0
            })
            .ToList();

        return ApiResult<List<SourceDistributionDto>>.Success(result);
    }

    public async Task<ApiResult<List<HandlerStatsDto>>> GetHandlerStatsAsync(long orgId, DateTime? startDate, DateTime? endDate, int top)
    {
        var start = startDate ?? DateTime.Now.AddDays(-30);
        var end = endDate ?? DateTime.Now;
        if (top <= 0) top = 10;

        var data = await _db.Set<QlException>()
            .Where(e => e.FOrgId == orgId && e.FAssigneeId != null && e.FCreateTime >= start && e.FCreateTime <= end)
            .Select(e => new { e.FAssigneeId, e.FStatus, e.FCreateTime, e.FClosedTime })
            .ToListAsync();

        // 内存中分组计算，避免复杂嵌套LINQ无法被SQL翻译
        var grouped = data
            .GroupBy(e => e.FAssigneeId!.Value)
            .Select(g =>
            {
                var closedWithTime = g.Where(e => e.FStatus == (int)ExceptionStatus.Closed && e.FClosedTime != null).ToList();
                var avgMin = closedWithTime.Count > 0
                    ? (double?)closedWithTime.Average(e => (e.FClosedTime!.Value - e.FCreateTime).TotalMinutes)
                    : null;
                return new
                {
                    UserId = g.Key,
                    HandleCount = g.Count(),
                    ClosedCount = g.Count(e => e.FStatus == (int)ExceptionStatus.Closed),
                    OverdueCount = g.Count(e => e.FStatus == (int)ExceptionStatus.Overdue),
                    AvgMin = avgMin
                };
            })
            .OrderByDescending(g => g.HandleCount)
            .Take(top)
            .ToList();

        // 获取用户名
        var userIds = grouped.Select(d => d.UserId).ToList();
        var userDict = await _db.Set<SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToDictionaryAsync(u => u.FID, u => u.FName);

        var result = grouped.Select(d => new HandlerStatsDto
        {
            UserId = d.UserId,
            UserName = userDict.TryGetValue(d.UserId, out var name) ? name : "未知",
            HandleCount = d.HandleCount,
            ClosedCount = d.ClosedCount,
            OverdueCount = d.OverdueCount,
            AvgResolutionHours = d.AvgMin.HasValue ? Math.Round(d.AvgMin.Value / 60.0, 1) : 0,
            OverdueRate = d.HandleCount > 0 ? Math.Round((double)d.OverdueCount / d.HandleCount * 100, 1) : 0
        }).ToList();

        return ApiResult<List<HandlerStatsDto>>.Success(result);
    }

    #region 枚举映射

    private static string MapExceptionType(int type) => type switch
    {
        0 => "数据异常",
        1 => "流程超时",
        2 => "规则违规",
        _ => "未知"
    };

    private static string MapExceptionStatus(int status) => status switch
    {
        0 => "待处理",
        1 => "处理中",
        2 => "已超时",
        3 => "已关闭",
        _ => "未知"
    };

    private static string MapPriority(int priority) => priority switch
    {
        0 => "低",
        1 => "中",
        2 => "高",
        3 => "紧急",
        _ => "未知"
    };

    #endregion
}
