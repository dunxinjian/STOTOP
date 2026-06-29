using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class ReturnToStageRuntimeTests
{
    [Fact]
    public void ResolvePreviousTarget_ChoosesNearestCompletedHumanStageInCurrentRound()
    {
        var runtime = new ReturnToStageRuntime();
        var stages = new List<CfStageDefinition>
        {
            new() { FID = 10, FSortOrder = 1, FType = "human" },
            new() { FID = 20, FSortOrder = 2, FType = "auto" },
            new() { FID = 30, FSortOrder = 3, FType = "human" },
            new() { FID = 40, FSortOrder = 4, FType = "human" }
        };
        var instances = new List<CfStageInstance>
        {
            new() { FID = 101, FStageDefinitionId = 10, FType = "human", FStatus = "completed", FRound = 2 },
            new() { FID = 102, FStageDefinitionId = 20, FType = "auto", FStatus = "completed", FRound = 2 },
            new() { FID = 103, FStageDefinitionId = 30, FType = "human", FStatus = "completed", FRound = 2 },
            new() { FID = 104, FStageDefinitionId = 30, FType = "human", FStatus = "completed", FRound = 1 }
        };

        var result = runtime.ResolvePreviousTarget(stages, instances, currentStageDefinitionId: 40, currentRound: 2, global::System.Array.Empty<CfRouteDecisionSnapshot>());

        Assert.True(result.Success);
        Assert.Equal(30, result.TargetStageDefinition!.FID);
        Assert.Equal(103, result.VisitedStageInstance!.FID);
    }

    [Fact]
    public void ResolveSpecifiedTarget_RejectsUnvisitedOrLaterTargets()
    {
        var runtime = new ReturnToStageRuntime();
        var stages = new List<CfStageDefinition>
        {
            new() { FID = 10, FSortOrder = 1, FType = "human" },
            new() { FID = 20, FSortOrder = 2, FType = "human" },
            new() { FID = 30, FSortOrder = 3, FType = "human" }
        };
        var instances = new List<CfStageInstance>
        {
            new() { FID = 101, FStageDefinitionId = 10, FType = "human", FStatus = "completed", FRound = 1 }
        };

        var unvisited = runtime.ResolveSpecifiedTarget(stages, instances, currentStageDefinitionId: 30, currentRound: 1, targetStageDefinitionId: 20, global::System.Array.Empty<CfRouteDecisionSnapshot>());
        var later = runtime.ResolveSpecifiedTarget(stages, instances, currentStageDefinitionId: 20, currentRound: 1, targetStageDefinitionId: 30, global::System.Array.Empty<CfRouteDecisionSnapshot>());

        Assert.False(unvisited.Success);
        Assert.Contains("未在当前轮次完成", unvisited.ErrorMessage);
        Assert.False(later.Success);
        Assert.Contains("当前节点之前", later.ErrorMessage);
    }

    [Fact]
    public void SupersedeDownstreamCompletedStages_CancelsCompletedHumanStagesAfterTarget()
    {
        var runtime = new ReturnToStageRuntime();
        var stages = new List<CfStageDefinition>
        {
            new() { FID = 10, FSortOrder = 1, FType = "human" },
            new() { FID = 20, FSortOrder = 2, FType = "human" },
            new() { FID = 30, FSortOrder = 3, FType = "auto" },
            new() { FID = 40, FSortOrder = 4, FType = "human" }
        };
        var instances = new List<CfStageInstance>
        {
            new() { FID = 101, FStageDefinitionId = 10, FType = "human", FStatus = "completed", FRound = 1 },
            new() { FID = 102, FStageDefinitionId = 20, FType = "human", FStatus = "completed", FRound = 1 },
            new() { FID = 103, FStageDefinitionId = 30, FType = "auto", FStatus = "completed", FRound = 1 },
            new() { FID = 104, FStageDefinitionId = 40, FType = "human", FStatus = "active", FRound = 1 }
        };

        var superseded = runtime.SupersedeDownstreamCompletedStages(stages, instances, targetStageDefinitionId: 10, currentRound: 1, currentStageDefinitionId: 40, global::System.Array.Empty<CfRouteDecisionSnapshot>());

        Assert.Equal(new long[] { 102 }, superseded.Select(s => s.FID));
        Assert.Equal("cancelled", instances.Single(i => i.FID == 102).FStatus);
        Assert.Equal("return-superseded", instances.Single(i => i.FID == 102).FFinalAction);
        Assert.Equal("completed", instances.Single(i => i.FID == 103).FStatus);
    }

    private static CfRouteDecisionSnapshot Edge(long from, long to, int seq, int round = 1) => new()
    {
        FID = seq,
        FFromStageDefinitionId = from,
        FToStageDefinitionId = to,
        FDecisionTime = new DateTime(2026, 6, 19, 0, 0, 0).AddSeconds(seq),
        FRound = round
    };

    // 非线性：manager(10,so=1) -> gm(30,so=3) -> finance(20,so=2)，从 finance 退回
    private static List<CfStageDefinition> NonLinearStages() => new()
    {
        new() { FID = 10, FSortOrder = 1, FType = "human" }, // manager
        new() { FID = 20, FSortOrder = 2, FType = "human" }, // finance (current)
        new() { FID = 30, FSortOrder = 3, FType = "human" }, // gm
    };

    private static List<CfStageInstance> NonLinearInstances() => new()
    {
        new() { FID = 101, FStageDefinitionId = 10, FType = "human", FStatus = "completed", FRound = 1 },
        new() { FID = 103, FStageDefinitionId = 30, FType = "human", FStatus = "completed", FRound = 1 },
        new() { FID = 102, FStageDefinitionId = 20, FType = "human", FStatus = "returned",  FRound = 1 },
    };

    private static List<CfRouteDecisionSnapshot> NonLinearSnapshots() => new()
    {
        Edge(10, 30, 1), // manager -> gm
        Edge(30, 20, 2), // gm -> finance
    };

    [Fact]
    public void ResolvePreviousTarget_RuleMode_UsesRealPathNotSortOrder()
    {
        var runtime = new ReturnToStageRuntime();

        var result = runtime.ResolvePreviousTarget(
            NonLinearStages(), NonLinearInstances(),
            currentStageDefinitionId: 20, currentRound: 1, NonLinearSnapshots());

        // 真实前驱是 gm(30)（sortOrder 3 > finance 的 2），sortOrder 口径会错选 manager(10)
        Assert.True(result.Success);
        Assert.Equal(30, result.TargetStageDefinition!.FID);
        Assert.Equal(103, result.VisitedStageInstance!.FID);
    }

    [Fact]
    public void ResolveSpecifiedTarget_RuleMode_AcceptsAncestorOnPath_RejectsOffPath()
    {
        var runtime = new ReturnToStageRuntime();

        // 指定 gm(30)：sortOrder 3 >= finance 2，旧口径会误拒；真实路径上居 finance 之前 → 成功
        var onPath = runtime.ResolveSpecifiedTarget(
            NonLinearStages(), NonLinearInstances(),
            currentStageDefinitionId: 20, currentRound: 1,
            targetStageDefinitionId: 30, NonLinearSnapshots());

        // 指定一个不在真实路径上的节点（50，本轮未完成且无边）→ 失败
        var stagesWithExtra = NonLinearStages();
        stagesWithExtra.Add(new CfStageDefinition { FID = 50, FSortOrder = 0, FType = "human" });
        var offPath = runtime.ResolveSpecifiedTarget(
            stagesWithExtra, NonLinearInstances(),
            currentStageDefinitionId: 20, currentRound: 1,
            targetStageDefinitionId: 50, NonLinearSnapshots());

        Assert.True(onPath.Success);
        Assert.Equal(30, onPath.TargetStageDefinition!.FID);
        Assert.False(offPath.Success);
        Assert.Contains("实际路径", offPath.ErrorMessage);
    }

    [Fact]
    public void SupersedeDownstream_RuleMode_CancelsByRealPathSegment()
    {
        var runtime = new ReturnToStageRuntime();
        var instances = NonLinearInstances();

        // 退回 manager(10)，current=finance(20)，真实路径 manager->gm->finance，
        // 作废段=path 上 manager 之后的已完成人工 = gm(103)（finance 实例状态 returned 不入集）
        var superseded = runtime.SupersedeDownstreamCompletedStages(
            NonLinearStages(), instances,
            targetStageDefinitionId: 10, currentRound: 1,
            currentStageDefinitionId: 20, NonLinearSnapshots());

        Assert.Equal(new long[] { 103 }, superseded.Select(s => s.FID));
        Assert.Equal("cancelled", instances.Single(i => i.FID == 103).FStatus);
        Assert.Equal("return-superseded", instances.Single(i => i.FID == 103).FFinalAction);
    }
}
