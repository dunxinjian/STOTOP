namespace STOTOP.Module.Conference;

/// <summary>
/// 会务管理模块权限编码常量
/// </summary>
public static class ConferencePermissions
{
    // 活动
    public const string EventView = "conf:event:view";
    public const string EventCreate = "conf:event:create";
    public const string EventEdit = "conf:event:edit";
    public const string EventDelete = "conf:event:delete";

    // 参会人员
    public const string AttendeeView = "conf:attendee:view";
    public const string AttendeeCreate = "conf:attendee:create";
    public const string AttendeeEdit = "conf:attendee:edit";
    public const string AttendeeDelete = "conf:attendee:delete";
    public const string AttendeeImport = "conf:attendee:import";
    public const string AttendeeExport = "conf:attendee:export";

    // 日程
    public const string ScheduleView = "conf:schedule:view";
    public const string ScheduleCreate = "conf:schedule:create";
    public const string ScheduleEdit = "conf:schedule:edit";
    public const string ScheduleDelete = "conf:schedule:delete";

    // 车辆与接送
    public const string TransportView = "conf:transport:view";
    public const string TransportCreate = "conf:transport:create";
    public const string TransportEdit = "conf:transport:edit";
    public const string TransportDelete = "conf:transport:delete";

    // 住宿
    public const string AccommodationView = "conf:accommodation:view";
    public const string AccommodationCreate = "conf:accommodation:create";
    public const string AccommodationEdit = "conf:accommodation:edit";
    public const string AccommodationDelete = "conf:accommodation:delete";

    // 餐食
    public const string MealView = "conf:meal:view";
    public const string MealCreate = "conf:meal:create";
    public const string MealEdit = "conf:meal:edit";
    public const string MealDelete = "conf:meal:delete";

    // 桌次编排
    public const string TableView = "conf:table:view";
    public const string TableEdit = "conf:table:edit";

    // 收入登记
    public const string IncomeView = "conf:income:view";
    public const string IncomeCreate = "conf:income:create";
    public const string IncomeEdit = "conf:income:edit";
    public const string IncomeDelete = "conf:income:delete";
    public const string IncomeExport = "conf:income:export";

    // 物品管理
    public const string MaterialView = "conf:material:view";
    public const string MaterialCreate = "conf:material:create";
    public const string MaterialEdit = "conf:material:edit";
    public const string MaterialDelete = "conf:material:delete";

    // 车辆日程
    public const string VehicleScheduleView = "conf:vehicle-schedule:view";
    public const string VehicleScheduleEdit = "conf:vehicle-schedule:edit";

    // 礼金管理
    public const string GiftView = "conf:gift:view";
    public const string GiftCreate = "conf:gift:create";
    public const string GiftEdit = "conf:gift:edit";
    public const string GiftDelete = "conf:gift:delete";
    public const string GiftExport = "conf:gift:export";

    // 典礼流程
    public const string CeremonyView = "conf:ceremony:view";
    public const string CeremonyCreate = "conf:ceremony:create";
    public const string CeremonyEdit = "conf:ceremony:edit";
    public const string CeremonyDelete = "conf:ceremony:delete";
}
