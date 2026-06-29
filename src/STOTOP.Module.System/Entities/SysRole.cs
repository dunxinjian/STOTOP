using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysRole : BaseEntity
{
    public string FName { get; set; } = string.Empty;
    public string FCode { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public int FStatus { get; set; } = 1;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual ICollection<SysUserRole> UserRoles { get; set; } = new List<SysUserRole>();
    public virtual ICollection<SysRolePermission> RolePermissions { get; set; } = new List<SysRolePermission>();
}
