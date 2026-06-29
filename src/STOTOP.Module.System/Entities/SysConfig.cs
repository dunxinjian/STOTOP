using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysConfig : BaseEntity
{
    public string FKey { get; set; } = string.Empty;
    public string FValue { get; set; } = string.Empty;
    public string FDataType { get; set; } = "string";
    public string? FDescription { get; set; }
    public bool FIsBuiltIn { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime? FUpdateTime { get; set; }
}
