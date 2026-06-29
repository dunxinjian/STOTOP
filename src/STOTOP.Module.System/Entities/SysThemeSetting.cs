using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysThemeSetting : BaseEntity
{
    public string FConfigJson { get; set; } = "{}";
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
    public string? FUpdateBy { get; set; }
}
