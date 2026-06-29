using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>日程物品</summary>
public class ConfScheduleItem : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FScheduleId { get; set; }
    public string FItemName { get; set; } = string.Empty;
    public int FQuantity { get; set; }
    public string? FUnit { get; set; }
    public string? FStatus { get; set; }
    public string? FRemark { get; set; }

    // Navigation
    public ConfSchedule Schedule { get; set; } = null!;
}
