using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Rules;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public sealed class StageRouteResolver : IStageRouteResolver
{
    private readonly STOTOPDbContext _dbContext;
    private readonly IConditionRuleEvaluator _conditionRuleEvaluator;
    private readonly IConditionEvaluationContextBuilder _contextBuilder;

    public StageRouteResolver(
        STOTOPDbContext dbContext,
        IConditionRuleEvaluator conditionRuleEvaluator,
        IConditionEvaluationContextBuilder contextBuilder)
    {
        _dbContext = dbContext;
        _conditionRuleEvaluator = conditionRuleEvaluator;
        _contextBuilder = contextBuilder;
    }

    public async Task<StageRouteResolveResult> ResolveNextStageAsync(
        CfCard card,
        CfStageInstance currentStage,
        CancellationToken cancellationToken = default)
    {
        var hasRules = await _dbContext.Set<CfStageRouteRule>()
            .AnyAsync(rule => rule.FFlowVersionId == card.FFlowVersionId && rule.FStatus == "active", cancellationToken);
        if (!hasRules)
        {
            return StageRouteResolveResult.LegacyFallback("legacy linear fallback: no route rules for this version");
        }

        var stages = await _dbContext.Set<CfStageDefinition>()
            .Where(stage => stage.FFlowVersionId == card.FFlowVersionId)
            .ToListAsync(cancellationToken);
        var currentDefinition = stages.FirstOrDefault(stage => stage.FID == currentStage.FStageDefinitionId);
        var fromStageKey = currentDefinition?.FStageKey ?? string.Empty;
        var fromStageId = currentDefinition?.FID ?? currentStage.FStageDefinitionId;

        var outgoing = await _dbContext.Set<CfStageRouteRule>()
            .Where(rule => rule.FFlowVersionId == card.FFlowVersionId
                && rule.FStatus == "active"
                && ((!string.IsNullOrWhiteSpace(fromStageKey) && rule.FFromStageKey == fromStageKey)
                    || (fromStageId != null && rule.FFromStageDefinitionId == fromStageId)))
            .OrderBy(rule => rule.FPriority)
            .ThenBy(rule => rule.FID)
            .ToListAsync(cancellationToken);

        var result = new StageRouteResolveResult
        {
            RuleMode = true,
            FromStageKey = fromStageKey,
            Reason = outgoing.Count == 0 ? "规则模式下当前节点没有出边，流程结束" : string.Empty
        };
        if (outgoing.Count == 0)
            return FinalizeResult(result, null);

        var context = await _contextBuilder.BuildAsync(card, currentStage, cancellationToken);
        CfStageRouteRule? selected = null;
        foreach (var rule in outgoing.Where(rule => !rule.FIsDefault))
        {
            if (string.IsNullOrWhiteSpace(rule.FConditionJson))
            {
                result.Candidates.Add(new StageRouteCandidateResult
                {
                    RouteRuleId = rule.FID,
                    EdgeKey = rule.FEdgeKey,
                    RouteName = rule.FRouteName,
                    ToStageKey = rule.FToStageKey,
                    Priority = rule.FPriority,
                    IsDefault = rule.FIsDefault,
                    Matched = false,
                    Explanation = "非默认分支缺条件，不命中",
                    TypeErrors = new List<string> { "非默认分支未配置条件" }
                });
                continue;
            }
            var evaluation = _conditionRuleEvaluator.Evaluate(rule.FConditionJson, context);
            result.Candidates.Add(new StageRouteCandidateResult
            {
                RouteRuleId = rule.FID,
                EdgeKey = rule.FEdgeKey,
                RouteName = rule.FRouteName,
                ToStageKey = rule.FToStageKey,
                Priority = rule.FPriority,
                IsDefault = rule.FIsDefault,
                Matched = evaluation.Matched,
                Explanation = evaluation.Explanation,
                TypeErrors = evaluation.TypeErrors,
                ConsumedFields = evaluation.ConsumedFields
            });
            if (evaluation.Matched)
            {
                selected = rule;
                result.Reason = evaluation.TypeErrors.Count == 0
                    ? $"命中条件：{rule.FRouteName}"
                    : $"命中条件：{rule.FRouteName}（注意：含类型错误的子条件，已忽略）";
                break;
            }
        }

        if (selected == null)
        {
            selected = outgoing.FirstOrDefault(rule => rule.FIsDefault);
            if (selected != null)
            {
                result.Candidates.Add(new StageRouteCandidateResult
                {
                    RouteRuleId = selected.FID,
                    EdgeKey = selected.FEdgeKey,
                    RouteName = selected.FRouteName,
                    ToStageKey = selected.FToStageKey,
                    Priority = selected.FPriority,
                    IsDefault = true,
                    Matched = true,
                    Explanation = "使用默认分支"
                });
                result.Reason = $"未命中条件，使用默认分支：{selected.FRouteName}";
            }
            else
            {
                result.Reason = "没有匹配条件且缺少默认分支，流程结束";
            }
        }

        result.SelectedRoute = selected;
        if (selected != null)
        {
            result.NextStage = stages.FirstOrDefault(stage => stage.FStageKey == selected.FToStageKey)
                ?? stages.FirstOrDefault(stage => stage.FID == selected.FToStageDefinitionId);
            if (result.NextStage == null)
            {
                result.Reason = $"目标节点不存在：{selected.FToStageKey}";
            }
        }

        return FinalizeResult(result, context);
    }

    private static StageRouteResolveResult FinalizeResult(StageRouteResolveResult result, ConditionEvaluationContext? context)
    {
        result.CandidateResultsJson = JsonSerializer.Serialize(result.Candidates);
        result.DecisionSnapshotJson = JsonSerializer.Serialize(new
        {
            result.RuleMode,
            result.FromStageKey,
            selectedEdgeKey = result.SelectedRoute?.FEdgeKey,
            toStageKey = result.NextStage?.FStageKey,
            result.Reason,
            candidates = result.Candidates,
            source = context?.SourceContext
        });
        return result;
    }
}
