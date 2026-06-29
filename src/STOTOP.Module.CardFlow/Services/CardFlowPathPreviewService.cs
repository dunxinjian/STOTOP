using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Rules;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public sealed class CardFlowPathPreviewService : ICardFlowPathPreviewService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly IConditionRuleEvaluator _conditionRuleEvaluator;
    private readonly IAuditSnapshotPolicyService _auditSnapshotPolicyService;

    public CardFlowPathPreviewService(
        STOTOPDbContext dbContext,
        IConditionRuleEvaluator conditionRuleEvaluator,
        IAuditSnapshotPolicyService auditSnapshotPolicyService)
    {
        _dbContext = dbContext;
        _conditionRuleEvaluator = conditionRuleEvaluator;
        _auditSnapshotPolicyService = auditSnapshotPolicyService;
    }

    public async Task<CardFlowPathPreviewDto> PreviewDraftVersionAsync(
        long definitionId,
        CardFlowPathPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var version = request.FlowVersionId.HasValue
            ? await _dbContext.Set<CfFlowVersion>()
                .FirstOrDefaultAsync(v => v.FID == request.FlowVersionId.Value && v.FFlowDefinitionId == definitionId, cancellationToken)
            : await _dbContext.Set<CfFlowVersion>()
                .Where(v => v.FFlowDefinitionId == definitionId && v.FStatus == "draft")
                .OrderByDescending(v => v.FVersionNumber)
                .FirstOrDefaultAsync(cancellationToken);

        version ??= await _dbContext.Set<CfFlowVersion>()
            .Where(v => v.FFlowDefinitionId == definitionId && v.FIsCurrentVersion)
            .OrderByDescending(v => v.FVersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (version == null)
            throw new InvalidOperationException("没有可预演的流程版本");

        var stages = await _dbContext.Set<CfStageDefinition>()
            .Where(stage => stage.FFlowVersionId == version.FID)
            .OrderBy(stage => stage.FSortOrder)
            .ThenBy(stage => stage.FID)
            .ToListAsync(cancellationToken);
        if (stages.Count == 0)
            throw new InvalidOperationException("流程未定义任何节点");

        var routes = await _dbContext.Set<CfStageRouteRule>()
            .Where(route => route.FFlowVersionId == version.FID && route.FStatus == "active")
            .OrderBy(route => route.FPriority)
            .ThenBy(route => route.FID)
            .ToListAsync(cancellationToken);
        var dynamicPolicies = await _dbContext.Set<CfDynamicStagePolicy>()
            .Where(policy => policy.FFlowVersionId == version.FID && policy.FStatus == "active")
            .OrderBy(policy => policy.FPriority)
            .ThenBy(policy => policy.FID)
            .ToListAsync(cancellationToken);

        var result = new CardFlowPathPreviewDto
        {
            FlowDefinitionId = definitionId,
            FlowVersionId = version.FID
        };
        var context = await BuildPreviewContextAsync(request, cancellationToken);
        var stageByKey = stages
            .Where(stage => !string.IsNullOrWhiteSpace(stage.FStageKey))
            .GroupBy(stage => stage.FStageKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var current = stages.First();
        var maxSteps = Math.Clamp(request.MaxSteps ?? 50, 1, 100);
        var visitCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < maxSteps && current != null; i++)
        {
            var currentKey = current.FStageKey;
            if (string.IsNullOrWhiteSpace(currentKey))
            {
                result.Warnings.Add($"节点 {current.FStageName} 缺少 StageKey，预演停止");
                break;
            }

            visitCounts.TryGetValue(currentKey, out var visits);
            if (visits >= 1)
            {
                result.Warnings.Add($"检测到循环路径：{currentKey}");
                break;
            }
            visitCounts[currentKey] = visits + 1;

            var step = new CardFlowPathPreviewStepDto
            {
                Order = result.Steps.Count + 1,
                StepType = "stage",
                StageKey = currentKey,
                StageName = current.FStageName,
                Type = current.FType
            };
            result.Steps.Add(step);

            var outgoing = routes
                .Where(route =>
                    (!string.IsNullOrWhiteSpace(currentKey)
                        && string.Equals(route.FFromStageKey, currentKey, StringComparison.OrdinalIgnoreCase))
                    || (route.FFromStageDefinitionId != null && route.FFromStageDefinitionId == current.FID))
                .OrderBy(route => route.FPriority)
                .ThenBy(route => route.FID)
                .ToList();

            CfStageRouteRule? selectedRoute = null;
            if (outgoing.Count > 0)
            {
                selectedRoute = SelectRoute(outgoing, context, step);
                if (selectedRoute == null)
                {
                    result.Warnings.Add($"节点 {currentKey} 没有命中条件且缺少默认分支");
                    break;
                }

                step.SelectedEdgeKey = selectedRoute.FEdgeKey;
                step.SelectedRouteName = selectedRoute.FRouteName;
                step.Reason = selectedRoute.FIsDefault
                    ? $"未命中条件，使用默认分支：{selectedRoute.FRouteName}"
                    : $"命中条件：{selectedRoute.FRouteName}";

                AddDynamicPolicyPreviewSteps(dynamicPolicies, currentKey, context, selectedRoute, result);

                if (!stageByKey.TryGetValue(selectedRoute.FToStageKey, out current))
                {
                    result.Warnings.Add($"条件边目标节点不存在：{selectedRoute.FToStageKey}");
                    break;
                }
                continue;
            }

            if (routes.Count > 0)
                break;

            var currentIndex = stages.FindIndex(stage => stage.FID == current.FID);
            current = currentIndex >= 0 && currentIndex + 1 < stages.Count
                ? stages[currentIndex + 1]
                : null;
        }

        if (result.Steps.Count >= maxSteps)
            result.Warnings.Add($"预演已达到最大步骤数 {maxSteps}");

        _ = _auditSnapshotPolicyService;
        return result;
    }

    private CfStageRouteRule? SelectRoute(
        List<CfStageRouteRule> outgoing,
        ConditionEvaluationContext context,
        CardFlowPathPreviewStepDto step)
    {
        foreach (var route in outgoing.Where(route => !route.FIsDefault))
        {
            if (string.IsNullOrWhiteSpace(route.FConditionJson))
            {
                step.Candidates.Add(new CardFlowPathPreviewCandidateDto
                {
                    EdgeKey = route.FEdgeKey,
                    RouteName = route.FRouteName,
                    ToStageKey = route.FToStageKey,
                    Priority = route.FPriority,
                    IsDefault = route.FIsDefault,
                    Matched = false,
                    Explanation = "非默认分支缺条件，不命中",
                    TypeErrors = new List<string> { "非默认分支未配置条件" }
                });
                continue;
            }

            var evaluation = _conditionRuleEvaluator.Evaluate(route.FConditionJson, context);
            step.Candidates.Add(ToCandidate(route, evaluation));
            if (evaluation.Matched)
                return route;
        }

        var defaultRoute = outgoing.FirstOrDefault(route => route.FIsDefault);
        if (defaultRoute != null)
        {
            step.Candidates.Add(new CardFlowPathPreviewCandidateDto
            {
                EdgeKey = defaultRoute.FEdgeKey,
                RouteName = defaultRoute.FRouteName,
                ToStageKey = defaultRoute.FToStageKey,
                Priority = defaultRoute.FPriority,
                IsDefault = true,
                Matched = true,
                Explanation = "使用默认分支"
            });
        }

        return defaultRoute;
    }

    private void AddDynamicPolicyPreviewSteps(
        List<CfDynamicStagePolicy> policies,
        string sourceStageKey,
        ConditionEvaluationContext context,
        CfStageRouteRule selectedRoute,
        CardFlowPathPreviewDto result)
    {
        foreach (var policy in policies.Where(policy =>
            string.Equals(policy.FSourceStageKey, sourceStageKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(policy.FTriggerTiming, "afterRouteBeforeTarget", StringComparison.OrdinalIgnoreCase)))
        {
            var evaluation = _conditionRuleEvaluator.Evaluate(policy.FConditionJson, context);
            if (!evaluation.Matched || evaluation.TypeErrors.Count > 0)
                continue;

            result.Steps.Add(new CardFlowPathPreviewStepDto
            {
                Order = result.Steps.Count + 1,
                StepType = "dynamic",
                StageKey = $"dynamic:{policy.FPolicyKey}",
                StageName = policy.FPolicyName,
                Type = "human",
                PolicyKey = policy.FPolicyKey,
                PolicyName = policy.FPolicyName,
                SelectedEdgeKey = selectedRoute.FEdgeKey,
                Reason = $"命中动态审批策略：{policy.FPolicyName}"
            });
            return;
        }
    }

    private static CardFlowPathPreviewCandidateDto ToCandidate(
        CfStageRouteRule route,
        ConditionRuleEvaluationResult evaluation)
    {
        return new CardFlowPathPreviewCandidateDto
        {
            EdgeKey = route.FEdgeKey,
            RouteName = route.FRouteName,
            ToStageKey = route.FToStageKey,
            Priority = route.FPriority,
            IsDefault = route.FIsDefault,
            Matched = evaluation.Matched,
            Explanation = evaluation.Explanation,
            TypeErrors = evaluation.TypeErrors
        };
    }

    private async Task<ConditionEvaluationContext> BuildPreviewContextAsync(
        CardFlowPathPreviewRequest request, CancellationToken cancellationToken)
    {
        var cardData = ParseObject(request.InitialDataJson);
        foreach (var pair in ParseObject(request.DataJson))
        {
            cardData[pair.Key] = pair.Value;
        }

        var context = ConditionContextFactory.Build(new ConditionContextInputs
        {
            CardData = cardData,
            SourceModule = request.SourceModule,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            InitiatorId = request.InitiatorId,
            OrgId = request.OrgId,
            HasCurrentStage = false
        });

        var orgRole = await OrgRoleContextResolver.ResolveAsync(
            _dbContext, request.OrgId ?? 0, request.InitiatorId, cancellationToken);
        context.OrgChain = orgRole.OrgChain;
        context.RoleCodes = orgRole.RoleCodes;
        context.RoleNames = orgRole.RoleNames;
        return context;
    }

    private static Dictionary<string, object?> ParseObject(string? json)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return result;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = ToPlainValue(property.Value);
            }
        }
        catch
        {
            return result;
        }

        return result;
    }

    private static object? ToPlainValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetDecimal(out var decimalValue)
                ? decimalValue
                : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ToPlainValue).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => ToPlainValue(property.Value), StringComparer.OrdinalIgnoreCase),
            _ => element.ToString()
        };
    }
}
