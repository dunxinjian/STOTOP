using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>房间入住(多对多中间表)</summary>
public class ConfRoomGuest : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FRoomId { get; set; }
    public long FAttendeeId { get; set; }

    // Navigation
    public ConfRoom Room { get; set; } = null!;
    public ConfAttendee Attendee { get; set; } = null!;
}
