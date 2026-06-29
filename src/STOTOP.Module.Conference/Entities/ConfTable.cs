using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>桌次</summary>
public class ConfTable : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FMealPlanId { get; set; }
    public int FTableNumber { get; set; }
    public string? FTableName { get; set; }
    public int FSeatCount { get; set; }
    public string? FRemark { get; set; }
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public ConfMealPlan MealPlan { get; set; } = null!;
    public List<ConfTableSeat> Seats { get; set; } = new();
}
