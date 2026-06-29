using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Contracts.Hr;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Services;

/// <summary>
/// IEmployeeOrgQueryService 的 System 模块实现。
/// 数据源：
///   - SYS用户组织（SysUserOrganization）— 组织维度任职记录（含 F生效起期 / F生效止期 / F是否当前）
///   - SYS用户岗位（SysUserPosition）— 用户授权岗位（F是否主岗）
/// </summary>
public class EmployeeOrgQueryService : IEmployeeOrgQueryService
{
    private readonly STOTOPDbContext _context;

    public EmployeeOrgQueryService(STOTOPDbContext context)
    {
        _context = context;
    }

    public async Task<long?> GetPrimaryOrgIdAsync(long userId)
    {
        return await _context.Set<SysUserOrganization>()
            .AsNoTracking()
            .Where(uo => uo.FUserId == userId
                         && uo.F是否当前
                         && uo.FIsPrimaryOrg == 1
                         && uo.FStatus == 1)
            .Select(uo => (long?)uo.FOrgId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<long>> GetAllOrgIdsAsync(long userId)
    {
        return await _context.Set<SysUserOrganization>()
            .AsNoTracking()
            .Where(uo => uo.FUserId == userId
                         && uo.F是否当前
                         && uo.FStatus == 1)
            .Select(uo => uo.FOrgId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<long?> GetPrimaryPositionIdAsync(long userId, long orgId)
    {
        // 当前实现：岗位授权未按组织二次划分（SysUserPosition 无 FOrgId），
        // 故 orgId 仅作为业务上下文参数；后续如新增 FOrgId 列，再增加过滤条件。
        return await _context.Set<SysUserPosition>()
            .AsNoTracking()
            .Where(up => up.FUserId == userId && up.FIsPrimary == 1)
            .Select(up => (long?)up.FPositionId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<long>> GetAllPositionIdsAsync(long userId, long orgId)
    {
        return await _context.Set<SysUserPosition>()
            .AsNoTracking()
            .Where(up => up.FUserId == userId)
            .Select(up => up.FPositionId)
            .Distinct()
            .ToListAsync();
    }

    /// <summary>
    /// BFS 递归查找直接 + 间接下属。
    /// 父子关系：SysUserOrganization.FDirectSuperiorId 指向上级 UserId；
    /// 限定同一 orgId + F是否当前=1 + F状态=1。
    /// 防御 self-loop / 环：visited 集合去重。
    /// </summary>
    public async Task<List<long>> GetSubordinateUserIdsAsync(long userId, long orgId)
    {
        var visited = new HashSet<long> { userId };
        var result = new List<long>();
        var frontier = new List<long> { userId };

        while (frontier.Count > 0)
        {
            var currentFrontier = frontier;
            var nextLevel = await _context.Set<SysUserOrganization>()
                .AsNoTracking()
                .Where(uo => uo.FOrgId == orgId
                             && uo.F是否当前
                             && uo.FStatus == 1
                             && uo.FDirectSuperiorId.HasValue
                             && currentFrontier.Contains(uo.FDirectSuperiorId.Value))
                .Select(uo => uo.FUserId)
                .Distinct()
                .ToListAsync();

            frontier = new List<long>();
            foreach (var uid in nextLevel)
            {
                if (visited.Add(uid))
                {
                    result.Add(uid);
                    frontier.Add(uid);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 取 atDate 落在 [F生效起期, ISNULL(F生效止期, '9999-12-31')] 区间内的记录；
    /// 若同区间存在多条，优先 F是否主组织=1 的记录，再按 F生效起期 倒序兜底。
    /// </summary>
    public async Task<EmployeeOrgSnapshotDto?> GetSnapshotAsync(long userId, long orgId, DateTime atDate)
    {
        var endSentinel = new DateTime(9999, 12, 31);

        var record = await _context.Set<SysUserOrganization>()
            .AsNoTracking()
            .Where(uo => uo.FUserId == userId
                         && uo.FOrgId == orgId
                         && uo.F生效起期 <= atDate
                         && (uo.F生效止期 ?? endSentinel) >= atDate)
            .OrderByDescending(uo => uo.FIsPrimaryOrg)
            .ThenByDescending(uo => uo.F生效起期)
            .FirstOrDefaultAsync();

        if (record == null)
            return null;

        var primaryPositionId = await _context.Set<SysUserPosition>()
            .AsNoTracking()
            .Where(up => up.FUserId == userId && up.FIsPrimary == 1)
            .Select(up => (long?)up.FPositionId)
            .FirstOrDefaultAsync();

        return new EmployeeOrgSnapshotDto
        {
            UserId = record.FUserId,
            OrgId = record.FOrgId,
            PrimaryPositionId = primaryPositionId,
            JobNumber = record.FJobNumber,
            EntryDate = record.FEntryDate,
            DirectSuperiorId = record.FDirectSuperiorId,
            IsPrimaryOrg = record.FIsPrimaryOrg == 1,
            EffectiveStartDate = record.F生效起期,
            EffectiveEndDate = record.F生效止期
        };
    }
}
