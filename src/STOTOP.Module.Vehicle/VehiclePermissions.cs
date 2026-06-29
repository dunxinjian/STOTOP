namespace STOTOP.Module.Vehicle;

public static class VehiclePermissions
{
    // 车辆台账
    public const string VehicleView = "vehicle:vehicle:view";
    public const string VehicleCreate = "vehicle:vehicle:create";
    public const string VehicleEdit = "vehicle:vehicle:edit";
    public const string VehicleDelete = "vehicle:vehicle:delete";
    // 车辆分配
    public const string AssignmentView = "vehicle:assignment:view";
    public const string AssignmentCreate = "vehicle:assignment:create";
    public const string AssignmentEdit = "vehicle:assignment:edit";
    // 租赁费用标准
    public const string RentalStandardView = "vehicle:rental-standard:view";
    public const string RentalStandardCreate = "vehicle:rental-standard:create";
    public const string RentalStandardEdit = "vehicle:rental-standard:edit";
    // 租赁收费
    public const string RentalChargeView = "vehicle:rental-charge:view";
    public const string RentalChargeCreate = "vehicle:rental-charge:create";
    public const string RentalChargeConfirm = "vehicle:rental-charge:confirm";
    public const string RentalChargeWaive = "vehicle:rental-charge:waive";
    // 维修
    public const string MaintenanceView = "vehicle:maintenance:view";
    public const string MaintenanceCreate = "vehicle:maintenance:create";
    public const string MaintenanceEdit = "vehicle:maintenance:edit";
    // GPS
    public const string GpsView = "vehicle:gps:view";
}
