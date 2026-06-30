using STOTOP.Core.Models;

namespace STOTOP.Module.Contract.Entities;

public class ConESignRecord : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FContractId { get; set; }
    public string FSigner { get; set; } = string.Empty;
    public string? FSignerRole { get; set; }
    public string? FSignMethod { get; set; }
    public int FSignStatus { get; set; }
    public DateTime? FSignedTime { get; set; }
    public string? FThirdPartyNo { get; set; }
    public string? FSignedFilePath { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public ConContract Contract { get; set; } = null!;
}
