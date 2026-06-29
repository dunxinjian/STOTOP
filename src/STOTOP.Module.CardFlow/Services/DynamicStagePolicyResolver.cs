using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Rules;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public sealed class DynamicStagePolicyResolver : IDynamicStagePolicyResolver
{
    private static readonly HashSet<string> SupportedStrategies = new(StringComparer.OrdinalIgnoreCase)
    {
        "fixedUsers",
        "fieldUsers",
        "role",
        "orgChain",
        "amountMatrix",
        "feeTypeBp"
    };

    private readonly STOTOPDbContext _dbContext;
    private readonly IConditionRuleEvaluator _conditionRuleEvaluator;
    private readonly IConditionEvaluationContextBuilder _contextBuilder;
    private readonly IApproverResolver _approverResolver;

    public DynamicStagePolicyResolver(
        STOTOPDbContext dbContext,
        IConditionRuleEvaluator conditionRuleEvaluator,
        IConditionEvaluationContextBuilder contextBuilder,
        IApproverResolver approverResolver)
    {
        _dbContext = dbContext;
        _conditionRuleEvaluator = conditionRuleEvaluator;
        _contextBuilder = contextBuilder;
        _approverResolver = approverResolver;
    }

    public async Task<DynamicStagePolicyResolveResult> ResolveBeforeTargetAsync(
        CfCard card,
        CfStageInstance sourceStage,
        StageRouteResolveResult routeResult,
        CancellationToken cancellationToken = default)
        => await ResolveAsync(card, sourceStage, "afterRouteBeforeTarget", routeResult, cancellationToken);

    public async Task<DynamicStagePolicyResolveResult> ResolveAsync(
        CfCard card,
        CfStageInstance sourceStage,
        string triggerTiming,
        StageRouteResolveResult? routeResult = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedTiming = NormalizeTriggerTiming(triggerTiming);
        if (string.IsNullOrWhiteSpace(normalizedTiming))
            return DynamicStagePolicyResolveResult.NoInsert("动态策略触发时机无效");

        if (sourceStage.FIsDynamicInsert)
            return DynamicStagePolicyResolveResult.NoInsert("动态节点自身不触发动态策略");

        if ((normalizedTiming == "afterRouteBeforeTarget" || normalizedTiming == "replaceTargetHandlers")
            && (routeResult == null || !routeResult.RuleMode || string.IsNullOrWhiteSpace(routeResult.FromStageKey)))
        {
            return DynamicStagePolicyResolveResult.NoInsert("非条件路由模式，不插入动态节点");
        }

        var sourceStageKey = routeResult?.FromStageKey;
        if (string.IsNullOrWhiteSpace(sourceStageKey))
        {
            sourceStageKey = await _dbContext.Set<CfStageDefinition>()
                .Where(stage => stage.FID == sourceStage.FStageDefinitionId)
                .Select(stage => stage.FStageKey)
                .FirstOrDefaultAsync(cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(sourceStageKey))
            return DynamicStagePolicyResolveResult.NoInsert("当前节点缺少 StageKey，无法匹配动态策略");

        var timingCandidates = normalizedTiming == "afterSourceBeforeRoute"
            ? new[] { normalizedTiming, "afterComplete" }
            : new[] { normalizedTiming };

        var policies = await _dbContext.Set<CfDynamicStagePolicy>()
            .Where(policy => policy.FFlowVersionId == card.FFlowVersionId
                && policy.FStatus == "active"
                && policy.FSourceStageKey == sourceStageKey
                && timingCandidates.Contains(policy.FTriggerTiming))
            .OrderBy(policy => policy.FPriority)
            .ThenBy(policy => policy.FID)
            .ToListAsync(cancellationToken);
        if (policies.Count == 0)
            return DynamicStagePolicyResolveResult.NoInsert($"没有 {normalizedTiming} 动态审批策略");

        var context = await _contextBuilder.BuildAsync(card, sourceStage, cancellationToken);
        var flowSettingsJson = await _dbContext.Set<CfFlowVersion>()
            .Where(version => version.FID == card.FFlowVersionId)
            .Select(version => version.FFlowSettingsJson)
            .FirstOrDefaultAsync(cancellationToken);
        foreach (var policy in policies)
        {
            var evaluation = _conditionRuleEvaluator.Evaluate(policy.FConditionJson, context);
            if (!evaluation.Matched || evaluation.TypeErrors.Count > 0)
                continue;

            if (!SupportedStrategies.Contains(policy.FStrategyType))
                return new DynamicStagePolicyResolveResult { ErrorMessage = $"不支持的动态审批策略：{policy.FStrategyType}" };

            var guard = await CheckInsertGuardAsync(card, sourceStage, policy, normalizedTiming, cancellationToken);
            if (!string.IsNullOrWhiteSpace(guard))
                return DynamicStagePolicyResolveResult.NoInsert(guard);

            var stageDefinition = new CfStageDefinition
            {
                FStageName = policy.FPolicyName,
                FType = "human",
                FApprovalMode = "single",
                FAssigneeStrategy = policy.FStrategyType,
                FAssigneeConfigJson = BuildAssigneeConfigJson(policy.FStrategyConfigJson, policy.FFallbackJson)
            };
            var approvers = await _approverResolver.ResolveAsync(
                stageDefinition,
                card,
                context.CardData,
                card.FOrgId,
                card.FInitiatorId,
                flowSettingsJson,
                cancellationToken: cancellationToken);
            if (!approvers.Success)
            {
                return new DynamicStagePolicyResolveResult
                {
                    Policy = policy,
                    ErrorMessage = approvers.ErrorMessage ?? $"动态策略 {policy.FPolicyKey} 未解析到处理人"
                };
            }

            var continuationStageKey = string.IsNullOrWhiteSpace(policy.FContinuationStageKey)
                ? routeResult?.NextStage?.FStageKey ?? string.Empty
                : policy.FContinuationStageKey;
            var shouldReplaceTargetHandlers = normalizedTiming == "replaceTargetHandlers";
            return new DynamicStagePolicyResolveResult
            {
                ShouldInsert = !shouldReplaceTargetHandlers,
                ShouldReplaceTargetHandlers = shouldReplaceTargetHandlers,
                Policy = policy,
                Approvers = approvers.Approvers,
                StageName = policy.FPolicyName,
                ApprovalMode = "single",
                TriggerTiming = normalizedTiming,
                InsertPosition = string.IsNullOrWhiteSpace(policy.FInsertPosition) ? "afterSource" : policy.FInsertPosition,
                ContinuationStageKey = continuationStageKey,
                Reason = $"命中动态审批策略：{policy.FPolicyName}",
                InsertContextJson = JsonSerializer.Serialize(new
                {
                    insertMode = "policy",
                    sourceStageInstanceId = sourceStage.FID,
                    sourceWasComplete = true,
                    policyKey = policy.FPolicyKey,
                    policyName = policy.FPolicyName,
                    sourceStageKey,
                    strategyType = policy.FStrategyType,
                    triggerTiming = normalizedTiming,
                    insertPosition = string.IsNullOrWhiteSpace(policy.FInsertPosition) ? "afterSource" : policy.FInsertPosition,
                    continuationStageKey,
                    selectedRouteEdgeKey = routeResult?.SelectedRoute?.FEdgeKey,
                    fallback = policy.FFallbackJson,
                    reason = $"命中动态审批策略：{policy.FPolicyName}",
                    approverIds = approvers.Approvers.Select(approver => approver.UserId),
                    approvers = approvers.Approvers.Select(approver => new
                    {
                        approver.UserId,
                        approver.Source,
                        approver.SortOrder
                    })
                })
            };
        }

        return DynamicStagePolicyResolveResult.NoInsert("没有命中的动态审批策略");
    }

    private async Task<string?> CheckInsertGuardAsync(
        CfCard card,
        CfStageInstance sourceStage,
        CfDynamicStagePolicy policy,
        string triggerTiming,
        CancellationToken cancellationToken)
    {
        if (triggerTiming == "replaceTargetHandlers")
            return null;

        var dynamicStages = await _dbContext.Set<CfStageInstance>()
            .Where(stage => stage.FCardId == card.FID && stage.FIsDynamicInsert && stage.FRound == card.FCurrentRound)
            .Select(stage => new { stage.FInsertSourceStageId, stage.FInsertContextJson })
            .ToListAsync(cancellationToken);
        if (dynamicStages.Count >= 20)
            return "单张卡片本轮动态审批节点已达到最大数量 20";

        var policyInsertCount = 0;
        foreach (var dynamicStage in dynamicStages)
        {
            var context = DynamicPolicyContext.TryParse(dynamicStage.FInsertContextJson);
            if (!string.Equals(context.PolicyKey, policy.FPolicyKey, StringComparison.OrdinalIgnoreCase))
                continue;

            policyInsertCount += 1;
            var sameSource = (context.SourceStageInstanceId ?? dynamicStage.FInsertSourceStageId) == sourceStage.FID;
            var sameTiming = string.Equals(context.TriggerTiming, triggerTiming, StringComparison.OrdinalIgnoreCase);
            if (sameSource && sameTiming)
            {
                return $"动态策略 {policy.FPolicyKey} 在当前节点本轮已插入";
            }
        }

        var maxInsertCount = Math.Clamp(policy.FMaxInsertCount <= 0 ? 20 : policy.FMaxInsertCount, 1, 20);
        if (policyInsertCount >= maxInsertCount)
            return $"动态策略 {policy.FPolicyKey} 已达到最大插入次数";

        return null;
    }

    private static string BuildAssigneeConfigJson(string? strategyConfigJson, string? fallbackJson)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(string.IsNullOrWhiteSpace(strategyConfigJson) ? "{}" : strategyConfigJson)?.AsObject()
                ?? new JsonObject();
        }
        catch (JsonException)
        {
            root = new JsonObject();
        }

        if (!string.IsNullOrWhiteSpace(fallbackJson))
        {
            try
            {
                var fallbackRoot = JsonNode.Parse(fallbackJson);
                if (fallbackRoot is JsonObject fallbackObject)
                {
                    root["fallback"] = fallbackObject.TryGetPropertyValue("fallback", out var nestedFallback)
                        ? nestedFallback?.DeepClone()
                        : fallbackObject.DeepClone();
                }
            }
            catch (JsonException)
            {
                root["fallback"] = new JsonObject { ["type"] = "failSubmit" };
            }
        }

        return root.ToJsonString();
    }

    private static string NormalizeTriggerTiming(string? triggerTiming)
    {
        return triggerTiming?.Trim() switch
        {
            "afterComplete" => "afterSourceBeforeRoute",
            "afterSourceBeforeRoute" => "afterSourceBeforeRoute",
            "afterRouteBeforeTarget" => "afterRouteBeforeTarget",
            "afterTarget" => "afterTarget",
            "replaceTargetHandlers" => "replaceTargetHandlers",
            _ => string.Empty
        };
    }

    private sealed class DynamicPolicyContext
    {
        public string? PolicyKey { get; private init; }
        public string? TriggerTiming { get; private init; }
        public long? SourceStageInstanceId { get; private init; }

        public static DynamicPolicyContext TryParse(string? contextJson)
        {
            if (string.IsNullOrWhiteSpace(contextJson))
                return new DynamicPolicyContext();

            try
            {
                using var document = JsonDocument.Parse(contextJson);
                var root = document.RootElement;
                return new DynamicPolicyContext
                {
                    PolicyKey = ReadString(root, "policyKey"),
                    TriggerTiming = ReadString(root, "triggerTiming"),
                    SourceStageInstanceId = ReadLong(root, "sourceStageInstanceId")
                };
            }
            catch (JsonException)
            {
                return new DynamicPolicyContext();
            }
        }

        private static string? ReadString(JsonElement root, string propertyName)
            => root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;

        private static long? ReadLong(JsonElement root, string propertyName)
            => root.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.Number
                && property.TryGetInt64(out var value)
                    ? value
                    : null;
    }
}
