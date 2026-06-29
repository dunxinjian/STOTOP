namespace STOTOP.Core.Contracts.Hr;

/// <summary>
/// 员工组织/岗位查询服务契约。
/// 由 STOTOP.Module.System 提供实现，供 KSF/PPV/Points 等业务模块以接口方式消费，
/// 避免业务模块反向依赖 HR/System 模块。
/// </summary>
public interface IEmployeeOrgQueryService
{
    /// <summary>
    /// 获取用户的主组织ID（F是否主组织=1，且 F是否当前=1）。
    /// </summary>
    Task<long?> GetPrimaryOrgIdAsync(long userId);

    /// <summary>
    /// 获取用户当前生效的所有组织ID（F是否当前=1）。
    /// </summary>
    Task<List<long>> GetAllOrgIdsAsync(long userId);

    /// <summary>
    /// 获取用户在指定组织下的主岗ID（SysUserPosition.F是否主岗=1）。
    /// </summary>
    Task<long?> GetPrimaryPositionIdAsync(long userId, long orgId);

    /// <summary>
    /// 获取用户在指定组织下授权的所有岗位ID。
    /// </summary>
    Task<List<long>> GetAllPositionIdsAsync(long userId, long orgId);

    /// <summary>
    /// 获取直属下属用户ID列表（递归）。
    /// 以 SysUserOrganization.F直接上级ID 为父子关系，限定 F组织ID=orgId 且 F是否当前=1。
    /// </summary>
    Task<List<long>> GetSubordinateUserIdsAsync(long userId, long orgId);

    /// <summary>
    /// 获取员工在指定组织、指定日期上的组织/岗位快照（用于 KSF/PPV 跨期重跑）。
    /// 选取 atDate 落在 [F生效起期, ISNULL(F生效止期, '9999-12-31')] 区间内的记录；
    /// 若同一区间内有多条，优先 F是否主组织=1 的记录。
    /// </summary>
    Task<EmployeeOrgSnapshotDto?> GetSnapshotAsync(long userId, long orgId, DateTime atDate);
}
