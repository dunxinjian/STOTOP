using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Entities;

public class PmPointSource : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public string FSourceName { get; set; } = string.Empty;
    public string FSourceCode { get; set; } = string.Empty;
    public string? FIcon { get; set; }
    public string? FColor { get; set; }
    public string? FDescription { get; set; }
    public int FSortOrder { get; set; }
    public bool FIsEnabled { get; set; } = true;
}
