using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services;

public sealed class ReturnToStageRuntime
{
    public ReturnToStageTargetResult ResolvePreviousTarget(
        IReadOnlyCollection<CfStageDefinition> stages,
        IReadOnlyCollection<CfStageInstance> stageInstances,
        long? currentStageDefinitionId,
        int currentRound,
        IReadOnlyCollection<CfRouteDecisionSnapshot> snapshots)
    {
        var currentDefinition = FindStage(stages, currentStageDefinitionId);
        if (currentDefinition == null)
            return ReturnToStageTargetResult.Fail("当前节点定义不存在");

        var stageById = stages.ToDictionary(s => s.FID);
        var path = RoutePathReconstructor.Reconstruct(snapshots, currentDefinition.FID);

        if (path.Count >= 2)
        {
            // 真实路径口径：current 之前最近的、本轮有 completed 非动态实例的人工节点
            for (var i = path.Count - 2; i >= 0; i--)
            {
                if (!stageById.TryGetValue(path[i], out var definition) || !IsHuman(definition))
                    continue;
                var instance = stageInstances
                    .Where(x => x.FRound == currentRound && x.FStatus == "completed"
                        && !x.FIsDynamicInsert && x.FStageDefinitionId == definition.FID)
                    .OrderByDescending(x => x.FID)
                    .FirstOrDefault();
                if (instance != null)
                    return ReturnToStageTargetResult.Ok(definition, instance);
            }
            return ReturnToStageTargetResult.Fail("未找到可退回的上一人工节点");
        }

        // 回退：按 sortOrder（线性流 / 无快照 / 遗留卡）
        var candidate = stageInstances
            .Where(instance =>
                instance.FRound == currentRound &&
                instance.FStatus == "completed" &&
                !instance.FIsDynamicInsert &&
                instance.FStageDefinitionId.HasValue &&
                stageById.TryGetValue(instance.FStageDefinitionId.Value, out var definition) &&
                IsHuman(definition) &&
                definition.FSortOrder < currentDefinition.FSortOrder)
            .Select(instance => new
            {
                Instance = instance,
                Definition = stageById[instance.FStageDefinitionId!.Value]
            })
            .OrderByDescending(x => x.Definition.FSortOrder)
            .ThenByDescending(x => x.Instance.FID)
            .FirstOrDefault();

        return candidate == null
            ? ReturnToStageTargetResult.Fail("未找到可退回的上一人工节点")
            : ReturnToStageTargetResult.Ok(candidate.Definition, candidate.Instance);
    }

    public ReturnToStageTargetResult ResolveSpecifiedTarget(
        IReadOnlyCollection<CfStageDefinition> stages,
        IReadOnlyCollection<CfStageInstance> stageInstances,
        long? currentStageDefinitionId,
        int currentRound,
        long? targetStageDefinitionId,
        IReadOnlyCollection<CfRouteDecisionSnapshot> snapshots)
    {
        if (!targetStageDefinitionId.HasValue)
            return ReturnToStageTargetResult.Fail("未指定退回目标节点");

        var currentDefinition = FindStage(stages, currentStageDefinitionId);
        if (currentDefinition == null)
            return ReturnToStageTargetResult.Fail("当前节点定义不存在");

        var targetDefinition = FindStage(stages, targetStageDefinitionId);
        if (targetDefinition == null)
            return ReturnToStageTargetResult.Fail("退回目标节点不属于当前流程版本");
        if (!IsHuman(targetDefinition))
            return ReturnToStageTargetResult.Fail("退回目标节点不是人工节点");

        var path = RoutePathReconstructor.Reconstruct(snapshots, currentDefinition.FID);
        if (path.Count >= 2)
        {
            // 真实路径口径：target 须在 current 之前（path 末元素为 current）
            var targetIdx = -1;
            for (var i = 0; i < path.Count; i++)
                if (path[i] == targetDefinition.FID) { targetIdx = i; break; }
            if (targetIdx < 0 || targetIdx >= path.Count - 1)
                return ReturnToStageTargetResult.Fail("退回目标不在当前节点的实际路径上");
        }
        else if (targetDefinition.FSortOrder >= currentDefinition.FSortOrder)
        {
            // 回退：sortOrder 校验
            return ReturnToStageTargetResult.Fail("退回目标节点必须在当前节点之前");
        }

        var visitedInstance = stageInstances
            .Where(instance =>
                instance.FRound == currentRound &&
                instance.FStatus == "completed" &&
                !instance.FIsDynamicInsert &&
                instance.FStageDefinitionId == targetDefinition.FID)
            .OrderByDescending(instance => instance.FID)
            .FirstOrDefault();

        return visitedInstance == null
            ? ReturnToStageTargetResult.Fail("退回目标节点未在当前轮次完成")
            : ReturnToStageTargetResult.Ok(targetDefinition, visitedInstance);
    }

    public IReadOnlyList<CfStageInstance> SupersedeDownstreamCompletedStages(
        IReadOnlyCollection<CfStageDefinition> stages,
        IReadOnlyCollection<CfStageInstance> stageInstances,
        long targetStageDefinitionId,
        int currentRound,
        long? currentStageDefinitionId,
        IReadOnlyCollection<CfRouteDecisionSnapshot> snapshots)
    {
        var targetDefinition = FindStage(stages, targetStageDefinitionId);
        if (targetDefinition == null)
            return Array.Empty<CfStageInstance>();

        var stageById = stages.ToDictionary(s => s.FID);

        // 真实路径口径：从 current 重建路径，取 target 之后那段为下游 def 集
        HashSet<long>? downstreamDefIds = null;
        if (currentStageDefinitionId.HasValue)
        {
            var path = RoutePathReconstructor.Reconstruct(snapshots, currentStageDefinitionId.Value);
            var targetIdx = -1;
            for (var i = 0; i < path.Count; i++)
                if (path[i] == targetStageDefinitionId) { targetIdx = i; break; }
            if (path.Count >= 2 && targetIdx >= 0)
                downstreamDefIds = path.Skip(targetIdx + 1).ToHashSet();
        }

        var superseded = downstreamDefIds != null
            ? stageInstances.Where(instance =>
                instance.FRound == currentRound &&
                instance.FStatus == "completed" &&
                !instance.FIsDynamicInsert &&
                instance.FStageDefinitionId.HasValue &&
                downstreamDefIds.Contains(instance.FStageDefinitionId.Value) &&
                stageById.TryGetValue(instance.FStageDefinitionId.Value, out var definition) &&
                IsHuman(definition)).ToList()
            // 回退：sortOrder > target（无 current / 无快照 / target 不在路径）
            : stageInstances.Where(instance =>
                instance.FRound == currentRound &&
                instance.FStatus == "completed" &&
                !instance.FIsDynamicInsert &&
                instance.FStageDefinitionId.HasValue &&
                stageById.TryGetValue(instance.FStageDefinitionId.Value, out var definition) &&
                IsHuman(definition) &&
                definition.FSortOrder > targetDefinition.FSortOrder).ToList();

        foreach (var instance in superseded)
        {
            instance.FStatus = "cancelled";
            instance.FFinalAction = "return-superseded";
            instance.FCompletedTime = DateTime.Now;
        }

        return superseded;
    }

    private static CfStageDefinition? FindStage(IEnumerable<CfStageDefinition> stages, long? id)
        => id.HasValue ? stages.FirstOrDefault(stage => stage.FID == id.Value) : null;

    private static bool IsHuman(CfStageDefinition stage)
        => string.Equals(stage.FType, "human", StringComparison.OrdinalIgnoreCase);
}

public sealed class ReturnToStageTargetResult
{
    public bool Success => string.IsNullOrWhiteSpace(ErrorMessage);
    public string? ErrorMessage { get; init; }
    public CfStageDefinition? TargetStageDefinition { get; init; }
    public CfStageInstance? VisitedStageInstance { get; init; }

    public static ReturnToStageTargetResult Ok(CfStageDefinition definition, CfStageInstance instance) => new()
    {
        TargetStageDefinition = definition,
        VisitedStageInstance = instance
    };

    public static ReturnToStageTargetResult Fail(string message) => new()
    {
        ErrorMessage = message
    };
}
