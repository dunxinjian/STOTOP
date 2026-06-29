using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysPermission : BaseEntity
{
    public string FName { get; set; } = string.Empty;
    public string FCode { get; set; } = string.Empty;
    public string FType { get; set; } = "菜单";
    public long FParentId { get; set; } = 0;
    public string? FRoute { get; set; }
    public string? FComponentPath { get; set; }
    public string? FIcon { get; set; }
    public int FSort { get; set; } = 0;
    public int FStatus { get; set; } = 1;
    public int FIsVisible { get; set; } = 1;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual ICollection<SysRolePermission> RolePermissions { get; set; } = new List<SysRolePermission>();
}
