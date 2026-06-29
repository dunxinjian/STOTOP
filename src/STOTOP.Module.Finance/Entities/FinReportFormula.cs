using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinReportFormula : BaseEntity
{
    public string FReportType { get; set; } = string.Empty;
    public string FItemName { get; set; } = string.Empty;
    public int FRowIndex { get; set; }
    public string? FFormula { get; set; }
    public string FFormulaType { get; set; } = string.Empty;
    public string? FAccountCodes { get; set; }
    public string? FDisplayConfig { get; set; }
    public bool FIsEnabled { get; set; } = true;
    public long FAccountSetId { get; set; }
    public int FSortOrder { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
