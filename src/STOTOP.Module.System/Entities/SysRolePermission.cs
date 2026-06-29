using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysRolePermission : BaseEntity
{
    public long FRoleId { get; set; }
    public long FPermissionId { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual SysRole Role { get; set; } = null!;
    public virtual SysPermission Permission { get; set; } = null!;
}
