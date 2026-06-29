using STOTOP.Core.Models;

namespace STOTOP.Module.Workflow.Entities;

/// <summary>WF问题明细 - 单个问题记录</summary>
public class WfIssueDetail : BaseEntity
{
    public long FIssuePackId { get; set; }
    public string? FDataScopeId { get; set; }
    public long? FRowId { get; set; }
    public string? FTableName { get; set; }
    public string FErrorType { get; set; } = string.Empty;
    public string? FErrorMessage { get; set; }
    public string? FFieldName { get; set; }
    public string? FOriginalValue { get; set; }
    public string? FCorrectedValue { get; set; }
    public bool FIsResolved { get; set; } = false;
    public long? FResolverId { get; set; }
    public DateTime? FResolvedTime { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual WfIssuePack? IssuePack { get; set; }
}
