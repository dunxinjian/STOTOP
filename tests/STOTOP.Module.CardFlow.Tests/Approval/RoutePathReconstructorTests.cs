using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class RoutePathReconstructorTests
{
    // 构造一条入边快照：from -> to，按 seq 递增 FDecisionTime / FID 表达先后
    private static CfRouteDecisionSnapshot Edge(long from, long to, int seq) => new()
    {
        FID = seq,
        FFromStageDefinitionId = from,
        FToStageDefinitionId = to,
        FDecisionTime = new DateTime(2026, 6, 19, 0, 0, 0).AddSeconds(seq),
        FRound = 1
    };

    [Fact]
    public void Reconstruct_LinearChain_ReturnsStartToCurrent()
    {
        var snapshots = new List<CfRouteDecisionSnapshot> { Edge(10, 20, 1), Edge(20, 30, 2) };

        var path = RoutePathReconstructor.Reconstruct(snapshots, currentStageDefinitionId: 30);

        Assert.Equal(new long[] { 10, 20, 30 }, path);
    }

    [Fact]
    public void Reconstruct_NonLinearBySortOrder_FollowsEdgesNotSortOrder()
    {
        // manager(10) -> gm(30) -> finance(20)：def id 顺序与执行顺序不一致
        var snapshots = new List<CfRouteDecisionSnapshot> { Edge(10, 30, 1), Edge(30, 20, 2) };

        var path = RoutePathReconstructor.Reconstruct(snapshots, currentStageDefinitionId: 20);

        Assert.Equal(new long[] { 10, 30, 20 }, path);
    }

    [Fact]
    public void Reconstruct_RetraversalWithDifferentBranch_UsesLatestEdge()
    {
        // 首次 manager->gm->finance；退回后改走 manager->director->finance
        var snapshots = new List<CfRouteDecisionSnapshot>
        {
            Edge(10, 30, 1),  // manager -> gm
            Edge(30, 20, 2),  // gm -> finance
            Edge(10, 40, 3),  // manager -> director (退回后重走)
            Edge(40, 20, 4),  // director -> finance
        };

        var path = RoutePathReconstructor.Reconstruct(snapshots, currentStageDefinitionId: 20);

        // 取最新入边：finance<-director<-manager，不含 gm(30)
        Assert.Equal(new long[] { 10, 40, 20 }, path);
    }

    [Fact]
    public void Reconstruct_Cycle_DoesNotLoopForever()
    {
        var snapshots = new List<CfRouteDecisionSnapshot> { Edge(10, 20, 1), Edge(20, 10, 2) };

        var path = RoutePathReconstructor.Reconstruct(snapshots, currentStageDefinitionId: 10);

        // 有界返回，不死循环；current=10 的最新入边是 20，20 的入边是 10(已访问)→停
        Assert.Equal(new long[] { 20, 10 }, path);
    }

    [Fact]
    public void Reconstruct_NoIncomingEdge_ReturnsOnlyCurrent()
    {
        var snapshots = new List<CfRouteDecisionSnapshot>(); // 空（线性流无快照）

        var path = RoutePathReconstructor.Reconstruct(snapshots, currentStageDefinitionId: 30);

        Assert.Equal(new long[] { 30 }, path);
    }
}
