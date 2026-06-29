using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysUserPosition : BaseEntity
{
    public long FUserId { get; set; }
    public long FPositionId { get; set; }
    public int FIsPrimary { get; set; }
    /// <summary>SysUser外键冗余列</summary>
    public long? SysUserFID { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual SysUser User { get; set; } = null!;
    public virtual SysPosition Position { get; set; } = null!;
}
