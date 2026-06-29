using STOTOP.Core.Models;

namespace STOTOP.Module.Workflow.Entities;

/// <summary>WF撤销日志 - 记录撤销操作影响</summary>
public class WfRevokeLog : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public string? FChainId { get; set; }
    public string? FDataScopeId { get; set; }
    public long FOperatorId { get; set; }
    public string? FOperatorName { get; set; }
    public string FRevokeType { get; set; } = string.Empty;
    public string? FTargetTable { get; set; }
    public int FAffectedRows { get; set; } = 0;
    public string? FRevokeStrategy { get; set; }
    public string? FSnapshot { get; set; }
    public bool FIsSuccess { get; set; } = true;
    public string? FErrorMessage { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
