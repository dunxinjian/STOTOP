using STOTOP.Core.Models;

namespace STOTOP.Module.Contract.Entities;

public class ConContractParty : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
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
