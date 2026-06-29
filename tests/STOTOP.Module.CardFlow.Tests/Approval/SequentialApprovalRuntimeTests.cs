using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class SequentialApprovalRuntimeTests
{
    [Fact]
    public void BuildInitialAssignments_MarksOnlyFirstSequentialAssigneePending()
    {
        var runtime = new SequentialApprovalRuntime();
        var approvers = new List<ResolvedApprover>
        {
            new() { UserId = 7, UserName = "C", SortOrder = 3 },
            new() { UserId = 5, UserName = "A", SortOrder = 1 },
            new() { UserId = 6, UserName = "B", SortOrder = 2 }
        };

        var assignments = runtime.BuildInitialAssignments("sequential", approvers);

        Assert.Equal(new long[] { 5, 6, 7 }, assignments.Select(a => a.UserId));
        Assert.Equal(new[] { "pending", "waiting", "waiting" }, assignments.Select(a => a.Status));
        Assert.Equal(new[] { 1, 2, 3 }, assignments.Select(a => a.SortOrder));
    }

    [Fact]
    public void PromoteNextWaitingAssignee_AdvancesInSortOrder()
    {
        var runtime = new SequentialApprovalRuntime();
        var assignees = new List<CfStageAssignee>
        {
            new() { FUserId = 1, FSortOrder = 1, FStatus = "approved" },
            new() { FUserId = 3, FSortOrder = 3, FStatus = "waiting" },
            new() { FUserId = 2, FSortOrder = 2, FStatus = "waiting" }
        };

        var promoted = runtime.PromoteNextWaitingAssignee(assignees);

        Assert.NotNull(promoted);
        Assert.Equal(2, promoted!.FUserId);
        Assert.Equal("pending", promoted.FStatus);
        Assert.Equal("waiting", assignees.Single(a => a.FUserId == 3).FStatus);
    }

    [Fact]
    public void CancelOpenAssignees_CancelsPendingAndWaitingOnly()
    {
        var runtime = new SequentialApprovalRuntime();
        var assignees = new List<CfStageAssignee>
        {
            new() { FUserId = 1, FStatus = "approved" },
            new() { FUserId = 2, FStatus = "pending" },
            new() { FUserId = 3, FStatus = "waiting" },
            new() { FUserId = 4, FStatus = "rejected" }
        };

        runtime.CancelOpenAssignees(assignees);

        Assert.Equal("approved", assignees[0].FStatus);
        Assert.Equal("cancelled", assignees[1].FStatus);
        Assert.Equal("cancelled", assignees[2].FStatus);
        Assert.Equal("rejected", assignees[3].FStatus);
    }
}
