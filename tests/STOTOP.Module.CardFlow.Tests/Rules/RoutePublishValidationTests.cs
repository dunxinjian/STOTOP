using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class RoutePublishValidationTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task Publish_NonDefaultRuleWithEmptyCondition_Throws()
    {
        using var db = TestDbContextFactory.Create(nameof(Publish_NonDefaultRuleWithEmptyCondition_Throws));
        var def = new CfFlowDefinition { FFlowName = "t", FFlowCode = "PUB1", FStatus = "draft" };
        db.Set<CfFlowDefinition>().Add(def);
        await db.SaveChangesAsync();
        var ver = new CfFlowVersion { FFlowDefinitionId = def.FID, FVersionNumber = 1, FStatus = "draft" };
        db.Set<CfFlowVersion>().Add(ver);
        await db.SaveChangesAsync();
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FFlowVersionId = ver.FID, FStageKey = "a", FStageName = "A", FSortOrder = 1 },
            new CfStageDefinition { FFlowVersionId = ver.FID, FStageKey = "b", FStageName = "B", FSortOrder = 2 });
        await db.SaveChangesAsync();
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule { FFlowVersionId = ver.FID, FEdgeKey = "e1", FFromStageKey = "a", FToStageKey = "b", FRouteName = "空", FConditionJson = null, FPriority = 1, FStatus = "active" },
            new CfStageRouteRule { FFlowVersionId = ver.FID, FEdgeKey = "e2", FFromStageKey = "a", FToStageKey = "b", FRouteName = "默认", FPriority = 99, FIsDefault = true, FStatus = "active" });
        await db.SaveChangesAsync();

        var svc = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var ex = await Assert.ThrowsAsync<global::System.InvalidOperationException>(() => svc.PublishAsync(def.FID, 1));
        Assert.Contains("非默认分支必须配置条件", ex.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Publish_NonDefaultRuleWithCondition_PassesRouteValidation()
    {
        using var db = TestDbContextFactory.Create(nameof(Publish_NonDefaultRuleWithCondition_PassesRouteValidation));
        var def = new CfFlowDefinition { FFlowName = "t", FFlowCode = "PUB2", FStatus = "draft" };
        db.Set<CfFlowDefinition>().Add(def);
        await db.SaveChangesAsync();
        var ver = new CfFlowVersion { FFlowDefinitionId = def.FID, FVersionNumber = 1, FStatus = "draft" };
        db.Set<CfFlowVersion>().Add(ver);
        await db.SaveChangesAsync();
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FFlowVersionId = ver.FID, FStageKey = "a", FStageName = "A", FSortOrder = 1 },
            new CfStageDefinition { FFlowVersionId = ver.FID, FStageKey = "b", FStageName = "B", FSortOrder = 2 });
        await db.SaveChangesAsync();
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule { FFlowVersionId = ver.FID, FEdgeKey = "e1", FFromStageKey = "a", FToStageKey = "b", FRouteName = "条件", FConditionJson = """{"field":"card.amount","operator":"gte","value":5000}""", FPriority = 1, FStatus = "active" },
            new CfStageRouteRule { FFlowVersionId = ver.FID, FEdgeKey = "e2", FFromStageKey = "a", FToStageKey = "b", FRouteName = "默认", FPriority = 99, FIsDefault = true, FStatus = "active" });
        await db.SaveChangesAsync();

        var svc = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var ex = await Record.ExceptionAsync(() => svc.PublishAsync(def.FID, 1));
        Assert.True(ex == null || !ex.Message.Contains("非默认分支必须配置条件"));
    }
}
