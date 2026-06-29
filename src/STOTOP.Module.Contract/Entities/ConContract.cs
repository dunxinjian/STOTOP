using STOTOP.Core.Models;

namespace STOTOP.Module.Contract.Entities;

public class ConContract : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public string FContractNo { get; set; } = string.Empty;
    public string FTitle { get; set; } = string.Empty;
    public long FTypeId { get; set; }
    public long? FTemplateId { get; set; }
    public decimal? FAmount { get; set; }
    public DateTime? FStartDate { get; set; }
    public DateTime? FEndDate { get; set; }
    public long? FRelatedContractId { get; set; }
    public int FContractNature { get; set; } = 1;
    public int FStatus { get; set; }
    public long? FOaProcessInstanceId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public ConContractType Type { get; set; } = null!;
    public ConContractTemplate? Template { get; set; }
    public List<ConContractParty> Parties { get; set; } = new();
    public List<ConContractClause> Clauses { get; set; } = new();
    public List<ConContractReminder> Reminders { get; set; } = new();
    public List<ConESignRecord> ESignRecords { get; set; } = new();
    public ConContract? RelatedContract { get; set; }
}
