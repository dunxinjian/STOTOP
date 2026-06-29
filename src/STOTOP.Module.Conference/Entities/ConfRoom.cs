using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>住宿房间</summary>
public class ConfRoom : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FHotelId { get; set; }
    public string? FRoomNumber { get; set; }
    public string? FRoomType { get; set; }
    public DateTime FCheckInDate { get; set; }
    public DateTime FCheckOutDate { get; set; }
    public string FStatus { get; set; } = "空闲";
    public string? FRemark { get; set; }
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public ConfHotel Hotel { get; set; } = null!;
    public List<ConfRoomGuest> RoomGuests { get; set; } = new();
}
