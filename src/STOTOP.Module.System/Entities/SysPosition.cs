using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysPosition : BaseEntity
{
    public string FUID { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public string FCode { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public int FStatus { get; set; } = 1;
    public string? FDingTalkPositionId { get; set; }
    public int FDingTalkBindStatus { get; set; }
    public int FSort { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual ICollection<SysPositionDepartment> PositionDepartments { get; set; } = new List<SysPositionDepartment>();
    public virtual ICollection<SysUserPosition> UserPositions { get; set; } = new List<SysUserPosition>();
}
