using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>餐食人员</summary>
public class ConfMealAttendee : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FMealPlanId { get; set; }
    public long FAttendeeId { get; set; }
    public string? FDietNote { get; set; }

    // Navigation
    public ConfMealPlan MealPlan { get; set; } = null!;
    public ConfAttendee Attendee { get; set; } = null!;
}
