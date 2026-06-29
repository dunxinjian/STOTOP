using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Services.Performance;

public class PerformanceService : IPerformanceService
{
    private readonly STOTOPDbContext _db;

    public PerformanceService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<PagedResult<PerformanceDto>>> GetPagedAsync(long orgId, PerformancePagedRequest request)
    {
        var query = _db.Set<QlPerformance>().Where(p => p.FOrgId == orgId);

        if (request.UserId.HasValue)
            query = query.Where(p => p.FUserId == request.UserId.Value);
        if (!string.IsNullOrWhiteSpace(request.Period))
            query = query.Where(p => p.FPeriod == request.Period);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.FPeriod)
            .ThenByDescending(p => p.FScore)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new PerformanceDto
            {
                Id = p.FID,
                UserId = p.FUserId,
                Period = p.FPeriod,
                ExceptionCount = p.FExceptionCount,
                ResolvedCount = p.FResolvedCount,
                OverdueCount = p.FOverdueCount,
                Score = p.FScore,
                Remark = p.FRemark,
                CreateTime = p.FCreateTime,
            })
            .ToListAsync();

        return ApiResult<PagedResult<PerformanceDto>>.Success(new PagedResult<PerformanceDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
        });
    }

    public async Task<ApiResult<PerformanceDto>> GetMyPerformanceAsync(long orgId, long userId, string period)
    {
        var entity = await _db.Set<QlPerformance>()
            .FirstOrDefaultAsync(p => p.FOrgId == orgId && p.FUserId == userId && p.FPeriod == period);

        if (entity == null)
            return ApiResult<PerformanceDto>.Fail("暂无绩效数据");

        return ApiResult<PerformanceDto>.Success(new PerformanceDto
        {
            Id = entity.FID,
            UserId = entity.FUserId,
            Period = entity.FPeriod,
            ExceptionCount = entity.FExceptionCount,
            ResolvedCount = entity.FResolvedCount,
            OverdueCount = entity.FOverdueCount,
            Score = entity.FScore,
            Remark = entity.FRemark,
            CreateTime = entity.FCreateTime,
        });
    }

    public async Task<ApiResult<PerformanceStatsDto>> GetStatsAsync(long orgId, long userId, string? period)
    {
        var query = _db.Set<QlPerformance>().Where(p => p.FOrgId == orgId);
        if (!string.IsNullOrWhiteSpace(period))
            query = query.Where(p => p.FPeriod == period);

        var totalHandled = await query.CountAsync();
        if (totalHandled == 0)
            return ApiResult<PerformanceStatsDto>.Success(new PerformanceStatsDto());

        var avgScore = (double)await query.AverageAsync(p => p.FScore);
        var maxScore = (double)await query.MaxAsync(p => p.FScore);
        var totalOverdue = await query.SumAsync(p => p.FOverdueCount);
        var totalExceptions = await query.SumAsync(p => p.FExceptionCount);
        var overdueRate = totalExceptions > 0 ? Math.Round((double)totalOverdue / totalExceptions * 100, 1) : 0;

        // 按用户聚合得分排名
        var userScores = await query
            .GroupBy(p => p.FUserId)
            .Select(g => new { UserId = g.Key, TotalScore = g.Sum(p => p.FScore) })
            .OrderByDescending(x => x.TotalScore)
            .ToListAsync();

        var totalUsers = userScores.Count;
        var myRank = userScores.FindIndex(x => x.UserId == userId) + 1; // 0 means not found -> rank 0

        return ApiResult<PerformanceStatsDto>.Success(new PerformanceStatsDto
        {
            AvgScore = Math.Round(avgScore, 2),
            MaxScore = Math.Round(maxScore, 2),
            TotalHandled = totalHandled,
            OverdueRate = overdueRate,
            MyRank = myRank,
            TotalUsers = totalUsers,
        });
    }

    public async Task<ApiResult<PagedResult<PerformanceRankingDto>>> GetRankingAsync(long orgId, string? period, int pageIndex, int pageSize)
    {
        var query = _db.Set<QlPerformance>().Where(p => p.FOrgId == orgId);
        if (!string.IsNullOrWhiteSpace(period))
            query = query.Where(p => p.FPeriod == period);

        var grouped = query
            .GroupBy(p => p.FUserId)
            .Select(g => new PerformanceRankingDto
            {
                UserId = g.Key,
                Score = (double)g.Sum(p => p.FScore),
                ExceptionCount = g.Sum(p => p.FExceptionCount),
                ResolvedCount = g.Sum(p => p.FResolvedCount),
                OverdueCount = g.Sum(p => p.FOverdueCount),
            });

        var total = await grouped.CountAsync();
        var items = await grouped
            .OrderByDescending(x => x.Score)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return ApiResult<PagedResult<PerformanceRankingDto>>.Success(new PagedResult<PerformanceRankingDto>
        {
            Items = items,
            Total = total,
            PageIndex = pageIndex,
            PageSize = pageSize,
        });
    }

    public async Task<ApiResult<List<PerformanceTrendDto>>> GetTrendAsync(long orgId, long userId)
    {
        var items = await _db.Set<QlPerformance>()
            .Where(p => p.FOrgId == orgId && p.FUserId == userId)
            .OrderBy(p => p.FPeriod)
            .Select(p => new PerformanceTrendDto
            {
                Period = p.FPeriod,
                Score = (double)p.FScore,
                HandleCount = p.FExceptionCount,
            })
            .ToListAsync();

        return ApiResult<List<PerformanceTrendDto>>.Success(items);
    }
}
