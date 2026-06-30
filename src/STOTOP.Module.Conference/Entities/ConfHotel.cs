using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>住宿酒店</summary>
public class ConfHotel : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FEventId { get; set; }
    public string FHotelName { get; set; } = string.Empty;
    public string? FAddress { get; set; }
    public string? FContact { get; set; }
    public string? FContactPhone { get; set; }
    public string? FAgreedPrice { get; set; }
    public string? FRemark { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public ConfEvent Event { get; set; } = null!;
    public List<ConfRoom> Rooms { get; set; } = new();
}
