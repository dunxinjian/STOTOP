using STOTOP.Core.Models;

namespace STOTOP.Module.Contract.Entities;

public class ConContractClause : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
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
