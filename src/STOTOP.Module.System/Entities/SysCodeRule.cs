using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysCodeRule : BaseEntity
{
    public string FRuleCode { get; set; } = string.Empty;
    public string FRuleName { get; set; } = string.Empty;
    public string FBusinessEntity { get; set; } = string.Empty;
    public string FCodeField { get; set; } = "F编码";
    public string? FPrefix { get; set; }
    public string? FDateFormat { get; set; }
    public int FSeqLength { get; set; } = 4;
    public string? FSeparator { get; set; } = "-";
    public string FResetCycle { get; set; } = "never";
    public bool FOrgIsolation { get; set; }
    public bool FEnabled { get; set; } = true;
    public string? FDescription { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual ICollection<SysCodeSequence> Sequences { get; set; } = new List<SysCodeSequence>();
}
