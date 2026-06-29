using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAccountSetRole : BaseEntity
{
    public string FName { get; set; } = "";
    public string FCode { get; set; } = "";
    public string? FDescription { get; set; }
    public bool FIsSystem { get; set; }
    public int FStatus { get; set; } = 1;
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
