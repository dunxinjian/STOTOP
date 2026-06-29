using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>餐食人员</summary>
public class ConfMealAttendee : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FMealPlanId { get; set; }
    public long FAttendeeId { get; set; }
    public string? FDietNote { get; set; }

    // Navigation
    public ConfMealPlan MealPlan { get; set; } = null!;
    public ConfAttendee Attendee { get; set; } = null!;
}
