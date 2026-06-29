using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>会务活动主表</summary>
public class ConfEvent : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public string FName { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public DateTime FStartDate { get; set; }
    public DateTime FEndDate { get; set; }
    public string? FLocation { get; set; }
    public string FStatus { get; set; } = "筹备中";
    public string? FManager { get; set; }
    public string? FManagerPhone { get; set; }
    public decimal FBudget { get; set; }
    public string? FRemark { get; set; }
    public string? FCreator { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    public string FType { get; set; } = "conference";  // 活动类型: conference/wedding
    public string? FGroomName { get; set; }  // 新郎姓名
    public string? FBrideName { get; set; }  // 新娘姓名

    // Navigation
    public List<ConfAttendee> Attendees { get; set; } = new();
    public List<ConfSchedule> Schedules { get; set; } = new();
    public List<ConfVehicle> Vehicles { get; set; } = new();
    public List<ConfPickupTask> PickupTasks { get; set; } = new();
    public List<ConfHotel> Hotels { get; set; } = new();
    public List<ConfMealPlan> MealPlans { get; set; } = new();
    public List<ConfIncome> Incomes { get; set; } = new();
    public List<ConfMaterial> Materials { get; set; } = new();
    public List<ConfVehicleSchedule> VehicleSchedules { get; set; } = new();
    public ICollection<ConfGift> Gifts { get; set; } = new List<ConfGift>();
    public ICollection<ConfCeremonyItem> CeremonyItems { get; set; } = new List<ConfCeremonyItem>();
}
