using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>住宿酒店</summary>
public class ConfHotel : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
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
