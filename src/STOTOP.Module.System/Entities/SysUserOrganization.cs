using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysUserOrganization : BaseEntity
{
    public long FUserId { get; set; }
    public long FOrgId { get; set; }
    public long? FDirectSuperiorId { get; set; }
    public int FIsPrimaryOrg { get; set; }
    public string? FPosition { get; set; }
    public string? FJobNumber { get; set; }
    public DateTime? FEntryDate { get; set; }
    public int FStatus { get; set; } = 1;
    public DateTime F生效起期 { get; set; } = DateTime.Now;
    public DateTime? F生效止期 { get; set; }
    public bool F是否当前 { get; set; } = true;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
    /// <summary>SysUser外键冗余列</summary>
    public long? SysUserFID { get; set; }

    // 导航属性
    public virtual SysUser User { get; set; } = null!;
    public virtual SysOrganization Organization { get; set; } = null!;
    public virtual SysUser? DirectSuperior { get; set; }
}
