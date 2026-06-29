using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAuxiliaryType : BaseEntity
{
    public string FName { get; set; } = string.Empty;
    public int FStatus { get; set; }
    public string FScope { get; set; } = "org_scoped";  // global / org_scoped
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
