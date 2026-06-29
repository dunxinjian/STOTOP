using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class StageRouteResolverTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task ResolveNextStage_UsesFirstMatchedPriorityRoute()
    {
        using var db = TestDbContextFactory.Create(nameof(ResolveNextStage_UsesFirstMatchedPriorityRoute));
        var stages = SeedStages(db);
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule
            {
                FFlowVersionId = 10,
                FEdgeKey = "large_amount",
                FFromStageDefinitionId = stages.Source.FID,
                FFromStageKey = "manager",
                FToStageDefinitionId = stages.GeneralManager.FID,
                FToStageKey = "gm",
                FRouteName = "大额报销",
                FConditionJson = """{"field":"card.amount","operator":"gte","value":5000}""",
                FPriority = 1,
                FStatus = "active"
            },
            new CfStageRouteRule
            {
                FFlowVersionId = 10,
                FEdgeKey = "default_finance",
                FFromStageDefinitionId = stages.Source.FID,
                FFromStageKey = "manager",
                FToStageDefinitionId = stages.Finance.FID,
                FToStageKey = "finance",
                FRouteName = "其他情况",
                FPriority = 99,
                FIsDefault = true,
                FStatus = "active"
            });
        await db.SaveChangesAsync();

        var resolver = new StageRouteResolver(db, new ConditionRuleEvaluator(), new ConditionEvaluationContextBuilder(db));
        var card = new CfCard { FID = 1, FFlowVersionId = 10, FDataJson = """{"amount":6800}""" };
        var current = new CfStageInstance { FID = 20, FStageDefinitionId = stages.Source.FID, FRound = 1 };

        var result = await resolver.ResolveNextStageAsync(card, current, CancellationToken.None);

        Assert.True(result.RuleMode);
        Assert.Equal("large_amount", result.SelectedRoute?.FEdgeKey);
        Assert.Equal(stages.GeneralManager.FID, result.NextStage?.FID);
        Assert.Contains(result.Candidates, candidate => candidate.EdgeKey == "large_amount" && candidate.Matched);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ResolveNextStage_UsesDefaultWhenNoConditionMatches()
    {
        using var db = TestDbContextFactory.Create(nameof(ResolveNextStage_UsesDefaultWhenNoConditionMatches));
        var stages = SeedStages(db);
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule
            {
                FFlowVersionId = 10,
                FEdgeKey = "large_amount",
                FFromStageKey = "manager",
                FToStageKey = "gm",
                FRouteName = "大额报销",
                FConditionJson = """{"field":"card.amount","operator":"gte","value":5000}""",
                FPriority = 1,
                FStatus = "active"
            },
            new CfStageRouteRule
            {
                FFlowVersionId = 10,
                FEdgeKey = "default_finance",
                FFromStageKey = "manager",
                FToStageKey = "finance",
                FRouteName = "其他情况",
                FPriority = 99,
                FIsDefault = true,
                FStatus = "active"
            });
        await db.SaveChangesAsync();

        var resolver = new StageRouteResolver(db, new ConditionRuleEvaluator(), new ConditionEvaluationContextBuilder(db));
        var card = new CfCard { FID = 1, FFlowVersionId = 10, FDataJson = """{"amount":1200}""" };
        var current = new CfStageInstance { FID = 20, FStageDefinitionId = stages.Source.FID, FRound = 1 };

        var result = await resolver.ResolveNextStageAsync(card, current, CancellationToken.None);

        Assert.True(result.RuleMode);
        Assert.Equal("default_finance", result.SelectedRoute?.FEdgeKey);
        Assert.Equal(stages.Finance.FID, result.NextStage?.FID);
        Assert.Contains("默认", result.Reason);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ResolveNextStage_ReturnsLegacyFallbackWhenVersionHasNoRoutes()
    {
        using var db = TestDbContextFactory.Create(nameof(ResolveNextStage_ReturnsLegacyFallbackWhenVersionHasNoRoutes));
        var stages = SeedStages(db);
        await db.SaveChangesAsync();

        var resolver = new StageRouteResolver(db, new ConditionRuleEvaluator(), new ConditionEvaluationContextBuilder(db));
        var card = new CfCard { FID = 1, FFlowVersionId = 10, FDataJson = """{"amount":1200}""" };
        var current = new CfStageInstance { FID = 20, FStageDefinitionId = stages.Source.FID, FRound = 1 };

        var result = await resolver.ResolveNextStageAsync(card, current, CancellationToken.None);

        Assert.False(result.RuleMode);
        Assert.Null(result.NextStage);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ResolveNextStage_NonDefaultEmptyCondition_DoesNotBecomeCatchAll()
    {
        using var db = TestDbContextFactory.Create(nameof(ResolveNextStage_NonDefaultEmptyCondition_DoesNotBecomeCatchAll));
        var stages = SeedStages(db);
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule
            {
                FFlowVersionId = 10, FEdgeKey = "empty_rule",
                FFromStageDefinitionId = stages.Source.FID, FFromStageKey = "manager",
                FToStageDefinitionId = stages.GeneralManager.FID, FToStageKey = "gm",
                FRouteName = "空条件", FConditionJson = null, FPriority = 1, FStatus = "active"
            },
            new CfStageRouteRule
            {
                FFlowVersionId = 10, FEdgeKey = "default_finance",
                FFromStageDefinitionId = stages.Source.FID, FFromStageKey = "manager",
                FToStageDefinitionId = stages.Finance.FID, FToStageKey = "finance",
                FRouteName = "默认", FPriority = 99, FIsDefault = true, FStatus = "active"
            });
        await db.SaveChangesAsync();

        var resolver = new StageRouteResolver(db, new ConditionRuleEvaluator(), new ConditionEvaluationContextBuilder(db));
        var card = new CfCard { FID = 1, FFlowVersionId = 10, FDataJson = """{"amount":6800}""" };
        var current = new CfStageInstance { FID = 20, FStageDefinitionId = stages.Source.FID, FRound = 1 };

        var result = await resolver.ResolveNextStageAsync(card, current, CancellationToken.None);

        Assert.Equal("default_finance", result.SelectedRoute?.FEdgeKey);
        Assert.Contains(result.Candidates, c => c.EdgeKey == "empty_rule" && !c.Matched);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ResolveNextStage_OrMatchedWithSiblingTypeError_StillSelectsRule()
    {
        using var db = TestDbContextFactory.Create(nameof(ResolveNextStage_OrMatchedWithSiblingTypeError_StillSelectsRule));
        var stages = SeedStages(db);
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule
            {
                FFlowVersionId = 10, FEdgeKey = "or_rule",
                FFromStageDefinitionId = stages.Source.FID, FFromStageKey = "manager",
                FToStageDefinitionId = stages.GeneralManager.FID, FToStageKey = "gm",
                FRouteName = "大额或类型错",
                // or: amount>=5000 (true) OR category>5 (string vs number → TypeError)
                FConditionJson = """{"logic":"or","conditions":[{"field":"card.amount","operator":"gte","value":5000},{"field":"card.category","operator":"gt","value":5}]}""",
                FPriority = 1, FStatus = "active"
            },
            new CfStageRouteRule
            {
                FFlowVersionId = 10, FEdgeKey = "default_finance",
                FFromStageDefinitionId = stages.Source.FID, FFromStageKey = "manager",
                FToStageDefinitionId = stages.Finance.FID, FToStageKey = "finance",
                FRouteName = "默认", FPriority = 99, FIsDefault = true, FStatus = "active"
            });
        await db.SaveChangesAsync();

        var resolver = new StageRouteResolver(db, new ConditionRuleEvaluator(), new ConditionEvaluationContextBuilder(db));
        var card = new CfCard { FID = 1, FFlowVersionId = 10, FDataJson = """{"amount":6800,"category":"travel"}""" };
        var current = new CfStageInstance { FID = 20, FStageDefinitionId = stages.Source.FID, FRound = 1 };

        var result = await resolver.ResolveNextStageAsync(card, current, CancellationToken.None);

        Assert.Equal("or_rule", result.SelectedRoute?.FEdgeKey);  // or 真命中(amount分支)→应选中,不被兄弟类型错毒化
        Assert.Equal(stages.GeneralManager.FID, result.NextStage?.FID);
    }

    private static (CfStageDefinition Source, CfStageDefinition Finance, CfStageDefinition GeneralManager) SeedStages(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        var source = new CfStageDefinition { FID = 101, FFlowVersionId = 10, FStageKey = "manager", FStageName = "主管审批", FSortOrder = 1 };
        var finance = new CfStageDefinition { FID = 102, FFlowVersionId = 10, FStageKey = "finance", FStageName = "财务复核", FSortOrder = 2 };
        var gm = new CfStageDefinition { FID = 103, FFlowVersionId = 10, FStageKey = "gm", FStageName = "总经理审批", FSortOrder = 3 };
        db.Set<CfStageDefinition>().AddRange(source, finance, gm);
        return (source, finance, gm);
    }
}
