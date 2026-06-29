using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>桌次座位</summary>
public class ConfTableSeat : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FTableId { get; set; }
    public long FAttendeeId { get; set; }
    public int FSeatNumber { get; set; }
    public string? FRemark { get; set; }

    // Navigation
    public ConfTable Table { get; set; } = null!;
    public ConfAttendee Attendee { get; set; } = null!;
}
