namespace STOTOP.Module.Dormitory;

/// <summary>
/// 宿舍管理模块权限编码常量
/// </summary>
public static class DormitoryPermissions
{
    // 楼栋权限
    public const string BuildingView = "dormitory:building:view";
    public const string BuildingCreate = "dormitory:building:create";
    public const string BuildingEdit = "dormitory:building:edit";
    public const string BuildingDelete = "dormitory:building:delete";

    // 房间权限
    public const string RoomView = "dormitory:room:view";
    public const string RoomCreate = "dormitory:room:create";
    public const string RoomEdit = "dormitory:room:edit";
    public const string RoomDelete = "dormitory:room:delete";

    // 床位权限
    public const string BedView = "dormitory:bed:view";
    public const string BedCreate = "dormitory:bed:create";
    public const string BedEdit = "dormitory:bed:edit";
    public const string BedDelete = "dormitory:bed:delete";

    // 入住记录权限
    public const string ResidenceView = "dormitory:residence:view";
    public const string ResidenceManage = "dormitory:residence:manage";

    // 费用记录权限
    public const string ExpenseView = "dormitory:expense:view";
    public const string ExpenseCreate = "dormitory:expense:create";
    public const string ExpenseEdit = "dormitory:expense:edit";
    public const string ExpenseDelete = "dormitory:expense:delete";

    // 设施权限
    public const string FacilityView = "dormitory:facility:view";
    public const string FacilityCreate = "dormitory:facility:create";
    public const string FacilityEdit = "dormitory:facility:edit";
    public const string FacilityDelete = "dormitory:facility:delete";

    // 报修工单权限
    public const string RepairView = "dormitory:repair:view";
    public const string RepairCreate = "dormitory:repair:create";
    public const string RepairHandle = "dormitory:repair:handle";

    // 访客登记权限
    public const string VisitorView = "dormitory:visitor:view";
    public const string VisitorCreate = "dormitory:visitor:create";
    public const string VisitorEdit = "dormitory:visitor:edit";

    // 卫生检查权限
    public const string HygieneView = "dormitory:hygiene:view";
    public const string HygieneCreate = "dormitory:hygiene:create";
}
