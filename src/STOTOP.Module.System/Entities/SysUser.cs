using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysUser : BaseEntity
{
    public string FUID { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public string FAccount { get; set; } = string.Empty;
    public string? FPhone { get; set; }
    public string? FEmail { get; set; }
    public string FPasswordHash { get; set; } = string.Empty;
    public string? FAvatar { get; set; }
    public int FStatus { get; set; } = 1;
    public string? FDingTalkUserId { get; set; }
    public int FDingTalkBindStatus { get; set; }
    public string? FDingTalkUserName { get; set; }
    public string? FDingTalkUnionId { get; set; }
    public string? FRemark { get; set; }
    /// <summary>布局偏好 JSON</summary>
    public string? F布局偏好 { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性 - 用于查询但不生成外键列
    public virtual ICollection<SysUserRole> UserRoles { get; set; } = new List<SysUserRole>();
    public virtual ICollection<SysUserOrganization> UserOrganizations { get; set; } = new List<SysUserOrganization>();
    public virtual ICollection<SysUserPosition> UserPositions { get; set; } = new List<SysUserPosition>();
}
