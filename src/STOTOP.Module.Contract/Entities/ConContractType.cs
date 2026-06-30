using STOTOP.Core.Models;

namespace STOTOP.Module.Contract.Entities;

public class ConContractType : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public string FName { get; set; } = string.Empty;
    public string FCode { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public int FSortOrder { get; set; }
    public int FStatus { get; set; } = 1;
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }
}
