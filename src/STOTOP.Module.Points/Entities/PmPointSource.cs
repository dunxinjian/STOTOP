using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Entities;

public class PmPointSource : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public string FSourceName { get; set; } = string.Empty;
    public string FSourceCode { get; set; } = string.Empty;
    public string? FIcon { get; set; }
    public string? FColor { get; set; }
    public string? FDescription { get; set; }
    public int FSortOrder { get; set; }
    public bool FIsEnabled { get; set; } = true;
}
