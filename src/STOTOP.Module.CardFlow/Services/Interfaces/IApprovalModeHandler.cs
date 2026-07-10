namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IApprovalModeHandler
{
    /// <summary>
    /// <paramref name="threshold"/> 仅 ratio 模式使用：1-99 百分比通过阈值；null 或越界时回退 100%（=countersign 语义）。
    /// 其余模式忽略该参数，保持向后兼容。
    /// </summary>
    bool IsStageCompleted(string approvalMode, List<AssigneeStatus> assignees, int? threshold = null);
    bool IsStageReturned(string approvalMode, List<AssigneeStatus> assignees, int? threshold = null);
}

public record AssigneeStatus(long UserId, string Status);
