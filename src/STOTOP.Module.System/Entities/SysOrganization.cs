using System.ComponentModel;
using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysOrganization : BaseEntity
{
    public string FUID { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public string FCode { get; set; } = string.Empty;
    public long FParentId { get; set; } = 0;

    /// <summary>组织类型ID，外键 -> SYS组织类型.FID</summary>
    public long FTypeId { get; set; } = 5; // 默认为部门

    /// <summary>组织类型字符串（兴容过渡期，请改用 FTypeId）</summary>
    [Obsolete("Use FTypeId instead")]
    public string FType { get; set; } = "部门";

    public int FSort { get; set; } = 0;
    public int FStatus { get; set; } = 1;
    public string? FDingTalkDeptId { get; set; }
    public int FDingTalkBindStatus { get; set; }
    public string? FDingTalkDeptName { get; set; }
    public long? FManagerId { get; set; }
    public int? FHeadcount { get; set; }
    public bool FIsSwitchable { get; set; }
    public string? FDescription { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual SysOrgType? OrgType { get; set; }
    public virtual SysUser? Manager { get; set; }
    public virtual ICollection<SysUserOrganization> UserOrganizations { get; set; } = new List<SysUserOrganization>();
    public virtual ICollection<SysPositionDepartment> PositionDepartments { get; set; } = new List<SysPositionDepartment>();
}
