using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAuxiliaryItem : BaseEntity, IOrgScoped
{
    public string FCode { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }

    // 扩展字段：账套维度辅助核算
    public long FAccountSetId { get; set; }
    public long FOrgId { get; set; }  // 组织ID
    public string? FAuxType { get; set; }  // customer/supplier/department/project/employee
    
    public string? FShortName { get; set; }
    public string? FContact { get; set; }  // 联系人
    public string? FPhone { get; set; }
    public string? FAddress { get; set; }
    public string? FRemark { get; set; }
    public int FEnableStatus { get; set; } = 1;  // 1启用 0停用

    // 来源字段：组织架构集成
    public string? FSourceType { get; set; }    // F来源类型: 'SYS组织架构' / 'SYS用户' / null(手动)
    public long? FSourceId { get; set; }         // F来源ID
}
