using STOTOP.Core.Models;

namespace STOTOP.Module.Vehicle.Entities;

public class VehVehicle : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public string FCode { get; set; } = string.Empty;        // 车辆编号
    public string? FPlateNumber { get; set; }                  // 车牌号
    public string? FBrand { get; set; }                        // 品牌型号
    public string? FFrameNumber { get; set; }                  // 车架号
    public int FOwnershipType { get; set; }                    // 权属类型: 1=公司, 2=员工个人
    public long? FOwnerId { get; set; }                        // 所有人ID
    public string? FOwnerName { get; set; }                    // 所有人姓名
    public DateTime? FPurchaseDate { get; set; }               // 购入日期
    public decimal? FPurchasePrice { get; set; }               // 购入价格
    public int FVehicleStatus { get; set; } = 1;               // 1=闲置, 2=使用中, 3=维修中, 4=报废
    public string? FColor { get; set; }                        // 颜色
    public string? FGpsDeviceNo { get; set; }                  // GPS设备号
    public string? FImage { get; set; }                        // 图片路径
    public string? FRemark { get; set; }
    public int FStatus { get; set; } = 1;
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
    // 导航属性
    public List<VehAssignment> Assignments { get; set; } = new();
    public List<VehMaintenance> Maintenances { get; set; } = new();
}
