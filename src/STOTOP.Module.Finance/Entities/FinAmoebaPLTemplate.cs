using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAmoebaPLTemplate : BaseEntity
{
    public string FName { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public int FIsDefault { get; set; }
    public long FAccountSetId { get; set; }  // 关联账套
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
    
    public List<FinAmoebaPLItem> Items { get; set; } = new();
}
