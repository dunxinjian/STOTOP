using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAssetCategory : BaseEntity, ITenantScoped
{
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FAccountSetId { get; set; }
    public string FCode { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public string FDepreciationMethod { get; set; } = string.Empty;
    public int FUsefulLife { get; set; }
    public decimal FResidualRate { get; set; }
    public long? FDepreciationAccountId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
