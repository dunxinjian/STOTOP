using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysPositionDepartment : BaseEntity
{
    public long FPositionId { get; set; }
    public long FOrganizationId { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual SysPosition Position { get; set; } = null!;
    public virtual SysOrganization Organization { get; set; } = null!;
}
