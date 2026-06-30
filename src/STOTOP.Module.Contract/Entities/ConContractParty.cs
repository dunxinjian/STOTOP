using STOTOP.Core.Models;

namespace STOTOP.Module.Contract.Entities;

public class ConContractParty : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FContractId { get; set; }
    public int FPartyRole { get; set; }
    public string? FRelatedBusinessType { get; set; }
    public long? FRelatedBusinessId { get; set; }
    public string FPartyName { get; set; } = string.Empty;
    public string? FContact { get; set; }
    public string? FPhone { get; set; }
    public string? FAddress { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public ConContract Contract { get; set; } = null!;
}
