using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;

namespace STOTOP.Module.CardFlow.Services;

public sealed class SequentialApprovalRuntime
{
    public IReadOnlyList<InitialAssigneeAssignment> BuildInitialAssignments(
        string? approvalMode,
        IReadOnlyList<ResolvedApprover> approvers)
    {
        var orderedApprovers = approvers
            .Select((approver, index) => new { Approver = approver, Index = index })
            .OrderBy(item => item.Approver.SortOrder > 0 ? item.Approver.SortOrder : item.Index + 1)
            .ToList();
        var isSequential = string.Equals(approvalMode, "sequential", StringComparison.OrdinalIgnoreCase);

        return orderedApprovers
            .Select((item, index) => new InitialAssigneeAssignment(
                item.Approver.UserId,
                item.Approver.UserName,
                index + 1,
                isSequential && index > 0 ? "waiting" : "pending"))
            .ToList();
    }

    public CfStageAssignee? PromoteNextWaitingAssignee(IEnumerable<CfStageAssignee> assignees)
    {
        var next = assignees
            .Where(assignee => assignee.FStatus == "waiting")
            .OrderBy(assignee => assignee.FSortOrder)
            .FirstOrDefault();

        if (next == null)
        {
            return null;
        }

        next.FStatus = "pending";
        next.FAssignedTime = DateTime.Now;
        return next;
    }

    public void CancelOpenAssignees(IEnumerable<CfStageAssignee> assignees)
    {
        foreach (var assignee in assignees.Where(a => a.FStatus is "pending" or "waiting"))
        {
            assignee.FStatus = "cancelled";
            assignee.FCompletedTime = DateTime.Now;
        }
    }
}

public sealed record InitialAssigneeAssignment(
    long UserId,
    string UserName,
    int SortOrder,
    string Status);
