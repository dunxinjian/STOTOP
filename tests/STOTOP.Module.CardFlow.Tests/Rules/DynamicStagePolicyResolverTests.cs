using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Rules;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class DynamicStagePolicyResolverTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task ResolveBeforeTarget_ReturnsFixedUsersWhenPolicyMatches()
    {
        using var db = TestDbContextFactory.Create(nameof(ResolveBeforeTarget_ReturnsFixedUsersWhenPolicyMatches));
        db.Set<SysUser>().Add(new SysUser { FID = 7, FName = "总经理", FStatus = 1 });
        db.Set<CfDynamicStagePolicy>().Add(new CfDynamicStagePolicy
        {
            FFlowVersionId = 10,
            FPolicyKey = "gm_approval",
            FSourceStageKey = "manager",
            FPolicyName = "大额追加总经理",
            FStrategyType = "fixedUsers",
            FStrategyConfigJson = """{"users":[{"userId":7,"userName":"总经理"}]}""",
            FConditionJson = """{"field":"card.amount","operator":"gte","value":5000}""",
            FTriggerTiming = "afterRouteBeforeTarget",
            FContinuationStageKey = "finance",
            FPriority = 1,
            FStatus = "active"
        });
        await db.SaveChangesAsync();

        var resolver = CreateResolver(db);
        var card = new CfCard { FID = 1, FFlowVersionId = 10, FOrgId = 100, FInitiatorId = 99, FDataJson = """{"amount":6800}""" };
        var source = new CfStageInstance { FID = 20, FStageDefinitionId = 101, FRound = 1 };
        var route = new StageRouteResolveResult { RuleMode = true, FromStageKey = "manager", NextStage = new CfStageDefinition { FStageKey = "finance" } };

        var result = await resolver.ResolveBeforeTargetAsync(card, source, route, CancellationToken.None);

        Assert.True(result.ShouldInsert);
        Assert.Equal("gm_approval", result.Policy?.FPolicyKey);
        Assert.Single(result.Approvers);
        Assert.Equal(7, result.Approvers[0].UserId);
        Assert.Contains("continuationStageKey", result.InsertContextJson);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ResolveBeforeTarget_ReturnsNoInsertWhenConditionDoesNotMatch()
    {
        using var db = TestDbContextFactory.Create(nameof(ResolveBeforeTarget_ReturnsNoInsertWhenConditionDoesNotMatch));
        db.Set<CfDynamicStagePolicy>().Add(new CfDynamicStagePolicy
        {
            FFlowVersionId = 10,
            FPolicyKey = "gm_approval",
            FSourceStageKey = "manager",
            FPolicyName = "大额追加总经理",
            FStrategyType = "fixedUsers",
            FStrategyConfigJson = """{"users":[{"userId":7,"userName":"总经理"}]}""",
            FConditionJson = """{"field":"card.amount","operator":"gte","value":5000}""",
            FTriggerTiming = "afterRouteBeforeTarget",
            FContinuationStageKey = "finance",
            FPriority = 1,
            FStatus = "active"
        });
        await db.SaveChangesAsync();

        var resolver = CreateResolver(db);
        var card = new CfCard { FID = 1, FFlowVersionId = 10, FOrgId = 100, FInitiatorId = 99, FDataJson = """{"amount":1200}""" };
        var source = new CfStageInstance { FID = 20, FStageDefinitionId = 101, FRound = 1 };
        var route = new StageRouteResolveResult { RuleMode = true, FromStageKey = "manager", NextStage = new CfStageDefinition { FStageKey = "finance" } };

        var result = await resolver.ResolveBeforeTargetAsync(card, source, route, CancellationToken.None);

        Assert.False(result.ShouldInsert);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ResolveBeforeTarget_UsesPolicyFallbackWhenPrimaryStrategyHasNoHandlers()
    {
        using var db = TestDbContextFactory.Create(nameof(ResolveBeforeTarget_UsesPolicyFallbackWhenPrimaryStrategyHasNoHandlers));
        db.Set<SysUser>().Add(new SysUser { FID = 8, FName = "兜底审批人", FStatus = 1 });
        db.Set<CfDynamicStagePolicy>().Add(new CfDynamicStagePolicy
        {
            FFlowVersionId = 10,
            FPolicyKey = "fallback_approval",
            FSourceStageKey = "manager",
            FPolicyName = "兜底加签",
            FStrategyType = "fixedUsers",
            FStrategyConfigJson = """{"users":[]}""",
            FConditionJson = """{"field":"card.amount","operator":"gte","value":5000}""",
            FTriggerTiming = "afterRouteBeforeTarget",
            FContinuationStageKey = "finance",
            FPriority = 1,
            FFallbackJson = """{"type":"fixedUsers","users":[{"userId":8,"userName":"兜底审批人"}]}""",
            FStatus = "active"
        });
        await db.SaveChangesAsync();

        var resolver = CreateResolver(db);
        var card = new CfCard { FID = 1, FFlowVersionId = 10, FOrgId = 100, FInitiatorId = 99, FDataJson = """{"amount":6800}""" };
        var source = new CfStageInstance { FID = 20, FStageDefinitionId = 101, FRound = 1 };
        var route = new StageRouteResolveResult { RuleMode = true, FromStageKey = "manager", NextStage = new CfStageDefinition { FStageKey = "finance" } };

        var result = await resolver.ResolveBeforeTargetAsync(card, source, route, CancellationToken.None);

        Assert.True(result.ShouldInsert);
        Assert.Single(result.Approvers);
        Assert.Equal(8, result.Approvers[0].UserId);
        Assert.Contains("fallback", result.InsertContextJson);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ResolveAsync_TreatsLegacyAfterCompleteAsAfterSourceBeforeRoute()
    {
        using var db = TestDbContextFactory.Create(nameof(ResolveAsync_TreatsLegacyAfterCompleteAsAfterSourceBeforeRoute));
        db.Set<SysUser>().Add(new SysUser { FID = 7, FName = "总经理", FStatus = 1 });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 101,
            FFlowVersionId = 10,
            FStageKey = "manager",
            FStageName = "主管审批",
            FType = "human"
        });
        db.Set<CfDynamicStagePolicy>().Add(new CfDynamicStagePolicy
        {
            FFlowVersionId = 10,
            FPolicyKey = "legacy_after_complete",
            FSourceStageKey = "manager",
            FPolicyName = "历史完成后加签",
            FStrategyType = "fixedUsers",
            FStrategyConfigJson = """{"users":[{"userId":7,"userName":"总经理"}]}""",
            FConditionJson = """{"field":"card.amount","operator":"gte","value":5000}""",
            FTriggerTiming = "afterComplete",
            FPriority = 1,
            FFallbackJson = """{"type":"fixedUsers","users":[{"userId":7,"userName":"总经理"}]}""",
            FStatus = "active"
        });
        await db.SaveChangesAsync();

        var resolver = CreateResolver(db);
        var card = new CfCard { FID = 1, FFlowVersionId = 10, FOrgId = 100, FInitiatorId = 99, FDataJson = """{"amount":6800}""" };
        var source = new CfStageInstance { FID = 20, FStageDefinitionId = 101, FRound = 1 };

        var result = await resolver.ResolveAsync(card, source, "afterSourceBeforeRoute", cancellationToken: CancellationToken.None);

        Assert.True(result.ShouldInsert);
        Assert.Equal("legacy_after_complete", result.Policy?.FPolicyKey);
        Assert.Equal("afterSourceBeforeRoute", result.TriggerTiming);
    }

    private static DynamicStagePolicyResolver CreateResolver(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        return new DynamicStagePolicyResolver(
            db,
            new ConditionRuleEvaluator(),
            new ConditionEvaluationContextBuilder(db),
            new ApproverResolver(db));
    }
}
