using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services;

public static class RoutePathReconstructor
{
    /// <summary>
    /// 入参 snapshots 已按 card + round 过滤。返回从起点到 current 的有序 def id 链（含 current 末尾）。
    /// 沿「每个节点的最新入边」(FDecisionTime 降序、FID 兜底) 回溯；current 无入边 → 仅返回 [current]。
    /// visited 集兼作去重与防环，遇环即停。
    /// </summary>
    public static IReadOnlyList<long> Reconstruct(
        IReadOnlyCollection<CfRouteDecisionSnapshot> snapshots,
        long currentStageDefinitionId)
    {
        var latestInto = snapshots
            .Where(s => s.FToStageDefinitionId.HasValue && s.FFromStageDefinitionId.HasValue)
            .GroupBy(s => s.FToStageDefinitionId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(s => s.FDecisionTime)
                    .ThenByDescending(s => s.FID)
                    .First().FFromStageDefinitionId!.Value);

        var reversed = new List<long> { currentStageDefinitionId };
        var visited = new HashSet<long> { currentStageDefinitionId };
        var node = currentStageDefinitionId;
        while (latestInto.TryGetValue(node, out var from) && visited.Add(from))
        {
            reversed.Add(from);
            node = from;
        }

        reversed.Reverse();
        return reversed;
    }
}
