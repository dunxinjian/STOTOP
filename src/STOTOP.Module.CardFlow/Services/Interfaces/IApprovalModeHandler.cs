namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IApprovalModeHandler
{
    bool IsStageCompleted(string approvalMode, List<AssigneeStatus> assignees);
    bool IsStageReturned(string approvalMode, List<AssigneeStatus> assignees);
}

public record AssigneeStatus(long UserId, string Status);
