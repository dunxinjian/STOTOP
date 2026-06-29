using STOTOP.Core.Models;

namespace STOTOP.Module.Contract.Entities;

public class ConContractClause : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FContractId { get; set; }
    public int FClauseOrder { get; set; }
    public string FClauseTitle { get; set; } = string.Empty;
    public string? FClauseContent { get; set; }
    public bool FIsKeyClause { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public ConContract Contract { get; set; } = null!;
}
