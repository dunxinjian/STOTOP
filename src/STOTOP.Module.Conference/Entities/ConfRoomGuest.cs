using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>房间入住(多对多中间表)</summary>
public class ConfRoomGuest : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FRoomId { get; set; }
    public long FAttendeeId { get; set; }

    // Navigation
    public ConfRoom Room { get; set; } = null!;
    public ConfAttendee Attendee { get; set; } = null!;
}
