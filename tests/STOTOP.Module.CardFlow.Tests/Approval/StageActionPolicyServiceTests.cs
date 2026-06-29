using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Models.Schema;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class StageActionPolicyServiceTests
{
    [Fact]
    public void ValidateAction_AllowsLegacyConfigWithoutPolicy()
    {
        var service = new StageActionPolicyService();

        var result = service.ValidateAction(new StageConfigEnvelope(), "transfer");

        Assert.True(result.Success);
    }

    [Fact]
    public void ValidateAction_RejectsActionOutsidePolicy()
    {
        var service = new StageActionPolicyService();
        var config = new StageConfigEnvelope
        {
            ActionPolicy = new StageActionPolicy
            {
                AllowedActions = new List<string> { "approve", "reject" }
            }
        };

        var result = service.ValidateAction(config, "transfer");

        Assert.False(result.Success);
        Assert.Equal("当前节点不允许执行操作: transfer", result.ErrorMessage);
    }

    [Fact]
    public void ValidateAction_AllowsConfiguredActionCaseInsensitively()
    {
        var service = new StageActionPolicyService();
        var config = new StageConfigEnvelope
        {
            ActionPolicy = new StageActionPolicy
            {
                AllowedActions = new List<string> { "Approve" }
            }
        };

        Assert.True(service.ValidateAction(config, "approve").Success);
    }
}
