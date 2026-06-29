using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>餐食计划</summary>
public class ConfMealPlan : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FEventId { get; set; }
    public DateTime FDate { get; set; }
    public string FMealType { get; set; } = string.Empty;
    public string? FDiningMode { get; set; }
    public string? FLocation { get; set; }
    public int FExpectedCount { get; set; }
    public int FActualCount { get; set; }
    public string? FRemark { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public ConfEvent Event { get; set; } = null!;
    public List<ConfMealAttendee> MealAttendees { get; set; } = new();
    public List<ConfTable> Tables { get; set; } = new();
}
