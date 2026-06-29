using STOTOP.Core.Models;

namespace STOTOP.Module.Contract.Entities;

public class ConESignRecord : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
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
