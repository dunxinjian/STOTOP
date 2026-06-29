using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class FlowDefinitionPublishGraphTests
{
    // 种一个含 active 规则的 rule-mode 流程草稿，返回 service + 流程定义 FID。
    private static (FlowDefinitionService Svc, long DefId) SeedFlow(
        STOTOP.Infrastructure.Data.STOTOPDbContext db,
        (string Key, int SortOrder)[] stages,
        (string From, string To)[] edges)
    {
        var def = new CfFlowDefinition { FFlowName = "图校验流程", FFlowCode = "GRAPH_" + global::System.Guid.NewGuid().ToString("N").Substring(0, 6), FStatus = "draft" };
        db.Set<CfFlowDefinition>().Add(def);
        db.SaveChanges();
        var version = new CfFlowVersion { FFlowDefinitionId = def.FID, FVersionNumber = 1, FStatus = "draft" };
        db.Set<CfFlowVersion>().Add(version);
        db.SaveChanges();
        foreach (var (key, sortOrder) in stages)
            db.Set<CfStageDefinition>().Add(new CfStageDefinition
            {
                FFlowVersionId = version.FID, FStageKey = key, FStageName = key.ToUpperInvariant(),
                FType = "human", FSortOrder = sortOrder
            });
        var i = 0;
        foreach (var (from, to) in edges)
            db.Set<CfStageRouteRule>().Add(new CfStageRouteRule
            {
                FFlowVersionId = version.FID, FEdgeKey = $"e{i++}", FFromStageKey = from, FToStageKey = to,
                FRouteName = $"{from}→{to}", FIsDefault = true, FPriority = 1, FStatus = "active"
            });
        db.SaveChanges();

        var svc = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        return (svc, def.FID);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Publish_RuleModeCycle_ThrowsWithCycleMessage()
    {
        using var db = TestDbContextFactory.Create(nameof(Publish_RuleModeCycle_ThrowsWithCycleMessage), 192);
        var (svc, defId) = SeedFlow(db,
            new[] { ("a", 1), ("b", 2) },
            new[] { ("a", "b"), ("b", "a") });   // 环

        var ex = await Assert.ThrowsAsync<global::System.InvalidOperationException>(() => svc.PublishAsync(defId, 1));
        Assert.Contains("环", ex.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Publish_RuleModeUnreachable_ThrowsWithUnreachableMessage()
    {
        using var db = TestDbContextFactory.Create(nameof(Publish_RuleModeUnreachable_ThrowsWithUnreachableMessage), 192);
        var (svc, defId) = SeedFlow(db,
            new[] { ("a", 1), ("b", 2), ("c", 3) },
            new[] { ("a", "b") });   // c 不可达

        var ex = await Assert.ThrowsAsync<global::System.InvalidOperationException>(() => svc.PublishAsync(defId, 1));
        Assert.Contains("不可达", ex.Message);
    }
}
