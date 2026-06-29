using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Interfaces;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class ApprovalModeHandlerTests
{
    [Fact]
    public void Single_Completes_WhenAnyAssigneeApproved()
    {
        var handler = new ApprovalModeHandler();
        var assignees = new List<AssigneeStatus>
        {
            new(1, "pending"),
            new(2, "approved")
        };

        Assert.True(handler.IsStageCompleted("single", assignees));
    }

    [Fact]
    public void Sequential_CompletesOnlyAfterAllActiveAssigneesApproved()
    {
        var handler = new ApprovalModeHandler();

        Assert.False(handler.IsStageCompleted("sequential", new List<AssigneeStatus>
        {
            new(1, "approved"),
            new(2, "waiting")
        }));
        Assert.False(handler.IsStageCompleted("sequential", new List<AssigneeStatus>
        {
            new(1, "approved"),
            new(2, "pending")
        }));
        Assert.True(handler.IsStageCompleted("sequential", new List<AssigneeStatus>
        {
            new(1, "approved"),
            new(2, "cancelled"),
            new(3, "transferred")
        }));
    }

    [Fact]
    public void Sequential_ReturnsWhenAnyActiveAssigneeRejected()
    {
        var handler = new ApprovalModeHandler();

        Assert.False(handler.IsStageReturned("sequential", new List<AssigneeStatus>
        {
            new(1, "approved"),
            new(2, "waiting")
        }));
        Assert.True(handler.IsStageReturned("sequential", new List<AssigneeStatus>
        {
            new(1, "approved"),
            new(2, "rejected")
        }));
        Assert.False(handler.IsStageReturned("sequential", new List<AssigneeStatus>
        {
            new(1, "cancelled"),
            new(2, "cancelled")
        }));
    }
}
