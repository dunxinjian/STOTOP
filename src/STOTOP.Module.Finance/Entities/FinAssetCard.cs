using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAssetCard : BaseEntity, ITenantScoped
{
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public string FCode { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public long FCategoryId { get; set; }
    public long? FDepartmentId { get; set; }
    public decimal FOriginalValue { get; set; }
    public decimal FAccumulatedDepreciation { get; set; }
    public decimal FNetValue { get; set; }
    public DateTime FEntryDate { get; set; }
    public DateTime? FStartDepreciationDate { get; set; }
    public int? FUsefulLife { get; set; }
    public decimal? FResidualRate { get; set; }
    public int FStatus { get; set; }
    public string? FRemark { get; set; }
    public long FOrgId { get; set; }  // 组织ID
    public long FAccountSetId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
