using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>礼金登记</summary>
public class ConfGift : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FEventId { get; set; }
    public long? FAttendeeId { get; set; }  // 关联主宾客（可空，手动输入时为null）
    public string? FGuestName { get; set; }  // 宾客姓名（手动输入时使用）
    public decimal FAmount { get; set; }
    public string? FGiftDescription { get; set; }
    public DateTime FRegistrationTime { get; set; } = DateTime.Now;
    public string FRegistrationMethod { get; set; } = "现金";  // 现金/转账/红包/礼物
    public bool FIsReturned { get; set; }  // 是否已回礼
    public string? FReturnContent { get; set; }
    public DateTime? FReturnTime { get; set; }
    public string? FRemark { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // 导航属性
    public ConfEvent Event { get; set; } = null!;
    public ConfAttendee? Attendee { get; set; }
}
