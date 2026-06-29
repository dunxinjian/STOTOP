using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>日程</summary>
public class ConfSchedule : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FEventId { get; set; }
    public DateTime FDate { get; set; }
    public TimeSpan FStartTime { get; set; }
    public TimeSpan FEndTime { get; set; }
    public string FTitle { get; set; } = string.Empty;
    public string? FLocation { get; set; }
    public string? FType { get; set; }
    public string? FDescription { get; set; }
    public int FSort { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public ConfEvent Event { get; set; } = null!;
    public List<ConfScheduleAttendee> ScheduleAttendees { get; set; } = new();
    public List<ConfScheduleItem> ScheduleItems { get; set; } = new();
    public List<ConfMaterial> Materials { get; set; } = new();
}
