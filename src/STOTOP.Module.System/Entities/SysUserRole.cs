using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysUserRole : BaseEntity
{
    public long FUserId { get; set; }
    public long FRoleId { get; set; }
    public long? FOrgId { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    /// <summary>SysUser外键冗余列</summary>
    public long? SysUserFID { get; set; }

    // 导航属性
    public virtual SysUser User { get; set; } = null!;
    public virtual SysRole Role { get; set; } = null!;
    public virtual SysOrganization? Organization { get; set; }
}
