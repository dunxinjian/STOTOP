using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Points.Services;

public class RankingService : IRankingService
{
    private readonly STOTOPDbContext _db;

    public RankingService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<PagedResult<RankingListDto>>> GetRankingsAsync(long orgId, RankingPagedRequest request)
    {
        var period = request.Period ?? GetCurrentPeriod(request.Dimension);

        var query = _db.Set<PmPointRanking>()
            .Where(r => r.FOrgId == orgId && r.FDimension == request.Dimension && r.FPeriod == period)
            .AsQueryable();

        var total = await query.CountAsync();

        var rankings = await query
            .OrderBy(r => r.FRank)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        // 批量获取用户名和部门名
        var userIds = rankings.Select(r => r.FUserId).Distinct().ToList();
        var users = await _db.Set<SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        var userDict = users.ToDictionary(u => u.FID, u => u.FName);

        var deptIds = rankings.Where(r => r.FDepartmentId.HasValue).Select(r => r.FDepartmentId!.Value).Distinct().ToList();
        var depts = await _db.Set<SysOrganization>()
            .Where(o => deptIds.Contains(o.FID))
            .Select(o => new { o.FID, o.FName })
            .ToListAsync();
        var deptDict = depts.ToDictionary(d => d.FID, d => d.FName);

        var items = rankings.Select(r => new RankingListDto
        {
            Id = r.FID,
            UserId = r.FUserId,
            UserName = userDict.GetValueOrDefault(r.FUserId),
            DepartmentName = r.FDepartmentId.HasValue ? deptDict.GetValueOrDefault(r.FDepartmentId.Value) : null,
            TotalPoints = r.FTotalPoints,
            AwardPoints = r.FAwardPoints,
            DeductPoints = r.FDeductPoints,
            Rank = r.FRank,
            Period = r.FPeriod,
            Dimension = r.FDimension
        }).ToList();

        return ApiResult<PagedResult<RankingListDto>>.Success(new PagedResult<RankingListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<List<DepartmentRankingDto>>> GetDepartmentRankingsAsync(long orgId, DepartmentRankingRequest request)
    {
        var period = request.Period ?? GetCurrentPeriod(request.Dimension);

        var rankings = await _db.Set<PmPointRanking>()
            .Where(r => r.FOrgId == orgId && r.FDimension == request.Dimension
                && r.FPeriod == period && r.FDepartmentId.HasValue)
            .ToListAsync();

        // 按部门分组统计
        var deptGroups = rankings
            .GroupBy(r => r.FDepartmentId!.Value)
            .Select(g => new
            {
                DepartmentId = g.Key,
                TotalPoints = g.Sum(r => r.FTotalPoints),
                AwardPoints = g.Sum(r => r.FAwardPoints),
                DeductPoints = g.Sum(r => r.FDeductPoints),
                MemberCount = g.Count()
            })
            .OrderByDescending(g => g.TotalPoints)
            .ToList();

        // 获取部门名
        var deptIds = deptGroups.Select(g => g.DepartmentId).ToList();
        var depts = await _db.Set<SysOrganization>()
            .Where(o => deptIds.Contains(o.FID))
            .Select(o => new { o.FID, o.FName })
            .ToListAsync();
        var deptDict = depts.ToDictionary(d => d.FID, d => d.FName);

        int rank = 1;
        var result = deptGroups.Select(g => new DepartmentRankingDto
        {
            DepartmentId = g.DepartmentId,
            DepartmentName = deptDict.GetValueOrDefault(g.DepartmentId) ?? "",
            TotalPoints = g.TotalPoints,
            AwardPoints = g.AwardPoints,
            DeductPoints = g.DeductPoints,
            MemberCount = g.MemberCount,
            AvgPoints = g.MemberCount > 0 ? Math.Round((decimal)g.TotalPoints / g.MemberCount, 2) : 0,
            Rank = rank++
        }).ToList();

        return ApiResult<List<DepartmentRankingDto>>.Success(result);
    }

    public async Task<ApiResult<MyRankingDto>> GetMyRankingAsync(long orgId, long userId, int dimension, string? period)
    {
        period ??= GetCurrentPeriod(dimension);

        var myRanking = await _db.Set<PmPointRanking>()
            .FirstOrDefaultAsync(r => r.FOrgId == orgId && r.FUserId == userId
                && r.FDimension == dimension && r.FPeriod == period);

        var totalUsers = await _db.Set<PmPointRanking>()
            .CountAsync(r => r.FOrgId == orgId && r.FDimension == dimension && r.FPeriod == period);

        var dto = new MyRankingDto
        {
            TotalPoints = myRanking?.FTotalPoints ?? 0,
            AwardPoints = myRanking?.FAwardPoints ?? 0,
            DeductPoints = myRanking?.FDeductPoints ?? 0,
            Rank = myRanking?.FRank ?? 0,
            TotalUsers = totalUsers,
            Period = period,
            Dimension = dimension
        };

        // 排名趋势（近6个周期）
        var recentPeriods = GetRecentPeriods(dimension, 6);

        var trends = await _db.Set<PmPointRanking>()
            .Where(r => r.FOrgId == orgId && r.FUserId == userId
                && r.FDimension == dimension && recentPeriods.Contains(r.FPeriod))
            .OrderBy(r => r.FPeriod)
            .ToListAsync();

        dto.Trends = trends.Select(t => new RankTrendItem
        {
            Period = t.FPeriod,
            TotalPoints = t.FTotalPoints,
            Rank = t.FRank
        }).ToList();

        return ApiResult<MyRankingDto>.Success(dto);
    }

    public async Task<ApiResult<bool>> GenerateSnapshotAsync(long orgId, int dimension, string period)
    {
        // 确定时间范围
        var (startTime, endTime) = GetPeriodRange(dimension, period);

        // 统计各用户的积分数据
        var records = await _db.Set<PmPointRecord>()
            .Where(r => r.FOrgId == orgId && r.FCreateTime >= startTime && r.FCreateTime < endTime)
            .ToListAsync();

        var userGroups = records
            .GroupBy(r => r.FUserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPoints = g.Sum(r => r.FPointValue),
                AwardPoints = g.Where(r => r.FType == 1).Sum(r => r.FPointValue),
                DeductPoints = g.Where(r => r.FType == 2).Sum(r => Math.Abs(r.FPointValue))
            })
            .OrderByDescending(g => g.TotalPoints)
            .ToList();

        // 获取用户部门信息
        var userIds = userGroups.Select(g => g.UserId).ToList();
        var userOrgs = await _db.Set<SysUserOrganization>()
            .Where(uo => userIds.Contains(uo.FUserId))
            .Select(uo => new { uo.FUserId, uo.FOrgId })
            .ToListAsync();
        var userDeptDict = userOrgs
            .GroupBy(uo => uo.FUserId)
            .ToDictionary(g => g.Key, g => g.First().FOrgId);

        // 使用事务保证删除旧快照和插入新快照的原子性
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // 删除旧的排名数据
            var oldRankings = await _db.Set<PmPointRanking>()
                .Where(r => r.FOrgId == orgId && r.FDimension == dimension && r.FPeriod == period)
                .ToListAsync();
            if (oldRankings.Count > 0)
                _db.Set<PmPointRanking>().RemoveRange(oldRankings);

            // 生成排名
            int rank = 1;
            foreach (var ug in userGroups)
            {
                var ranking = new PmPointRanking
                {
                    FOrgId = orgId,
                    FUserId = ug.UserId,
                    FDepartmentId = userDeptDict.GetValueOrDefault(ug.UserId),
                    FDimension = dimension,
                    FPeriod = period,
                    FTotalPoints = ug.TotalPoints,
                    FAwardPoints = ug.AwardPoints,
                    FDeductPoints = ug.DeductPoints,
                    FRank = rank++,
                    FGenerateTime = DateTime.Now
                };
                _db.Set<PmPointRanking>().Add(ranking);
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return ApiResult<bool>.Success(true, $"已生成排名快照，共{userGroups.Count}条");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 获取当前周期标识
    /// </summary>
    private static string GetCurrentPeriod(int dimension) => dimension switch
    {
        0 => DateTime.Now.ToString("yyyy-MM"),    // 月度
        1 => $"{DateTime.Now.Year}-Q{(DateTime.Now.Month - 1) / 3 + 1}", // 季度
        2 => DateTime.Now.Year.ToString(),         // 年度
        _ => DateTime.Now.ToString("yyyy-MM")
    };

    /// <summary>
    /// 获取近N个周期的标识列表
    /// </summary>
    private static List<string> GetRecentPeriods(int dimension, int count)
    {
        var periods = new List<string>();
        var now = DateTime.Now;

        for (int i = count - 1; i >= 0; i--)
        {
            switch (dimension)
            {
                case 0: // 月度
                    periods.Add(now.AddMonths(-i).ToString("yyyy-MM"));
                    break;
                case 1: // 季度
                    var date = now.AddMonths(-i * 3);
                    periods.Add($"{date.Year}-Q{(date.Month - 1) / 3 + 1}");
                    break;
                case 2: // 年度
                    periods.Add((now.Year - i).ToString());
                    break;
            }
        }

        return periods;
    }

    /// <summary>
    /// 获取周期的时间范围
    /// </summary>
    private static (DateTime Start, DateTime End) GetPeriodRange(int dimension, string period)
    {
        switch (dimension)
        {
            case 0: // 月度 yyyy-MM
                if (DateTime.TryParse(period + "-01", out var monthStart))
                    return (monthStart, monthStart.AddMonths(1));
                break;
            case 1: // 季度 yyyy-Qn
                var parts = period.Split("-Q");
                if (parts.Length == 2 && int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var quarter))
                {
                    var qStart = new DateTime(year, (quarter - 1) * 3 + 1, 1);
                    return (qStart, qStart.AddMonths(3));
                }
                break;
            case 2: // 年度 yyyy
                if (int.TryParse(period, out var y))
                {
                    var yearStart = new DateTime(y, 1, 1);
                    return (yearStart, yearStart.AddYears(1));
                }
                break;
        }

        // 默认当月
        var now = DateTime.Now;
        var defaultStart = new DateTime(now.Year, now.Month, 1);
        return (defaultStart, defaultStart.AddMonths(1));
    }
}
