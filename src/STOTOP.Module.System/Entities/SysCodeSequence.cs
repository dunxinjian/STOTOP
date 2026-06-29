using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysCodeSequence : BaseEntity
{
    public long FRuleId { get; set; }
    public long? FOrgId { get; set; }
    public string FPeriodKey { get; set; } = string.Empty;
    public long FCurrentValue { get; set; }
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual SysCodeRule Rule { get; set; } = null!;
}
