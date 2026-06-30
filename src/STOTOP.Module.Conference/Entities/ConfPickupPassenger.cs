using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>接送乘客(多对多中间表)</summary>
public class ConfPickupPassenger : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FPickupTaskId { get; set; }
    public long FAttendeeId { get; set; }

    // Navigation
    public ConfPickupTask PickupTask { get; set; } = null!;
    public ConfAttendee Attendee { get; set; } = null!;
}
