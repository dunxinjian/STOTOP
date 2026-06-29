using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>日程参会人(多对多中间表)</summary>
public class ConfScheduleAttendee : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FScheduleId { get; set; }
    public long FAttendeeId { get; set; }

    // Navigation
    public ConfSchedule Schedule { get; set; } = null!;
    public ConfAttendee Attendee { get; set; } = null!;
}
