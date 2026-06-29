using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>参会人员</summary>
public class ConfAttendee : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FEventId { get; set; }
    public string FName { get; set; } = string.Empty;
    public string? FGender { get; set; }
    public string? FPhone { get; set; }
    public string? FOrganization { get; set; }
    public string? FTitle { get; set; }
    public string? FRole { get; set; }
    public string? FDietPreference { get; set; }
    public string? FArrivalMode { get; set; }
    public string? FArrivalFlightTrain { get; set; }
    public DateTime? FArrivalTime { get; set; }
    public string? FArrivalStation { get; set; }
    public string? FDepartureMode { get; set; }
    public string? FDepartureFlightTrain { get; set; }
    public DateTime? FDepartureTime { get; set; }
    public string? FDepartureStation { get; set; }
    public bool FNeedPickup { get; set; } = true;
    public bool FNeedAccommodation { get; set; } = true;
    public string? FPreferredRoomType { get; set; }  // 房型偏好：标单/标双/套房/大床房/行政大床/其他
    public string? FRemark { get; set; }
    public string FStatus { get; set; } = "待确认";
    public string FCheckInStatus { get; set; } = "未签到";
    /// <summary>确认状态</summary>
    public string F确认状态 { get; set; } = "待联系";
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    public long? FPrimaryGuestId { get; set; }  // 主宾客ID（自引用，NULL=主宾客）
    public string? FRelation { get; set; }  // 关系：配偶/子女/父母/兄弟姐妹/其他
    public bool FIsChild { get; set; }  // 是否儿童
    public int? FAge { get; set; }  // 年龄
    public string? FCamp { get; set; }  // 阵营：男方/女方/共同
    public string? FGuestType { get; set; }  // 宾客类型：亲属/同学/同事/领导/朋友/世交
    public int FCompanionCount { get; set; }  // 随行人数
    public bool FHasSeat { get; set; } = true;  // 是否占座
    public string FMealCategory { get; set; } = "全餐";  // 餐标类别：全餐/儿童餐/不用餐

    // Navigation
    public ConfEvent Event { get; set; } = null!;
    public List<ConfScheduleAttendee> ScheduleAttendees { get; set; } = new();
    public List<ConfPickupPassenger> PickupPassengers { get; set; } = new();
    public List<ConfRoomGuest> RoomGuests { get; set; } = new();
    public List<ConfMealAttendee> MealAttendees { get; set; } = new();
    public List<ConfTableSeat> TableSeats { get; set; } = new();
    public List<ConfIncome> Incomes { get; set; } = new();

    // 自引用导航属性
    public ConfAttendee? PrimaryGuest { get; set; }
    public ICollection<ConfAttendee> Companions { get; set; } = new List<ConfAttendee>();

    // 礼金导航
    public ICollection<ConfGift> Gifts { get; set; } = new List<ConfGift>();
}
