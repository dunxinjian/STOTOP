using STOTOP.Core.Models;
using STOTOP.Module.Workflow.Enums;

namespace STOTOP.Module.Workflow.Entities;

/// <summary>WF派发规则 - 配置事件如何路由到处理人</summary>
public class WfDispatchRule : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public string FName { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public string? FModule { get; set; }
    public string? FBizType { get; set; }
    public int FDispatchMode { get; set; } = (int)DispatchMode.Auto;
    public string? FAutoAssignRule { get; set; }
    public int FTimeout { get; set; } = 0;
    public string? FEscalationRule { get; set; }
    public int FPriority { get; set; } = 0;
    public bool FIsEnabled { get; set; } = true;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
