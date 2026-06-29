namespace STOTOP.Core.Contracts.Hr;

/// <summary>
/// 员工在指定组织、指定日期上的组织/岗位快照。
/// </summary>
public class EmployeeOrgSnapshotDto
{
    /// <summary>
    /// 用户ID（SysUser.FID）。
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 组织ID（SysOrganization.FID）。
    /// </summary>
    public long OrgId { get; set; }

    /// <summary>
    /// 主岗ID（SysUserPosition.FPositionId where F是否主岗 = 1），无主岗时为 null。
    /// </summary>
    public long? PrimaryPositionId { get; set; }

    /// <summary>
    /// 工号（SysUserOrganization.F工号），可为空。
    /// </summary>
    public string? JobNumber { get; set; }

    /// <summary>
    /// 入职日期（SysUserOrganization.F入职日期）。
    /// </summary>
    public DateTime? EntryDate { get; set; }

    /// <summary>
    /// 直接上级用户ID（SysUserOrganization.F直接上级ID）。
    /// </summary>
    public long? DirectSuperiorId { get; set; }

    /// <summary>
    /// 是否为主组织。
    /// </summary>
    public bool IsPrimaryOrg { get; set; }

    /// <summary>
    /// 该快照对应记录的生效起期（F生效起期）。
    /// </summary>
    public DateTime EffectiveStartDate { get; set; }

    /// <summary>
    /// 该快照对应记录的生效止期（F生效止期），为空表示当前生效。
    /// </summary>
    public DateTime? EffectiveEndDate { get; set; }
}
