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
    private readonly IApproverResolver _approverResolver;

    public CardFlowPathPreviewService(
        STOTOPDbContext dbContext,
        IConditionRuleEvaluator conditionRuleEvaluator,
        IAuditSnapshotPolicyService auditSnapshotPolicyService,
        IApproverResolver approverResolver)
    {
        _dbContext = dbContext;
        _conditionRuleEvaluator = conditionRuleEvaluator;
        _auditSnapshotPolicyService = auditSnapshotPolicyService;
        _approverResolver = approverResolver;
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
        var cardData = BuildCardData(request);
        var context = await BuildPreviewContextAsync(request, cardData, cancellationToken);
        // 处理人干跑用的合成卡片（ApproverResolver 只读 cardData，不读 card 字段；此处仅补 org/initiator 兜底）
        var previewCard = new CfCard { FInitiatorId = request.InitiatorId ?? 0, FOrgId = request.OrgId ?? 0 };
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

            // 人工节点：干跑 ApproverResolver，回答"该节点将派给谁"（复用运行时解析，样例数据驱动）
            if (string.Equals(current.FType, "human", StringComparison.OrdinalIgnoreCase))
            {
                step.Approver = await ResolveApproverPreviewAsync(current, cardData, previewCard, request, version.FFlowSettingsJson, cancellationToken);

                // 失败态推演①：处理人解析失败且触发兜底 → 标注但不终止，继续推演
                if (step.Approver != null && !string.IsNullOrWhiteSpace(step.Approver.FallbackReason))
                {
                    step.Failure = new StepFailureDto
                    {
                        Kind = "assigneeUnresolved",
                        Message = step.Approver.FallbackReason!,
                        FallbackApplied = true
                    };
                }
            }
            // 自动节点：失败态推演③——静态可预判失败（未配插件/插件注册缺失/规则缺失或禁用），不真跑插件
            else if (IsAutoStage(current))
            {
                step.Failure = await PredictAutoStageFailureAsync(current, cancellationToken);
            }

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
                    // 失败态推演②：出边均不命中且无默认兜底 → 无路可走，流程在此结束（终点）
                    step.Failure = new StepFailureDto
                    {
                        Kind = "noBranchMatch",
                        Message = "无匹配分支且无兜底，流程在此结束",
                        FallbackApplied = false
                    };
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

    // 兼容旧版 FType="batchAuto" 与新版 FType="auto"（口径同 FlowEngineService.IsBatchAutoStage）
    private static bool IsAutoStage(CfStageDefinition stage)
        => string.Equals(stage.FType, "auto", StringComparison.OrdinalIgnoreCase)
        || string.Equals(stage.FType, "batchAuto", StringComparison.OrdinalIgnoreCase);

    // 失败态推演③：自动节点静态可预判失败——只查配置完整性（未配插件/插件注册缺失/规则缺失或禁用），
    // 不真跑插件（诚实：运行时才知的失败不模拟）。无法静态预判则返回 null。
    private async Task<StepFailureDto?> PredictAutoStageFailureAsync(
        CfStageDefinition stage, CancellationToken cancellationToken)
    {
        var failurePolicyFallback = HasFailureFallback(stage.FFailurePolicyJson);

        if (!stage.F插件注册ID.HasValue)
        {
            return new StepFailureDto
            {
                Kind = "autoStageError",
                Message = "自动节点未配置插件（插件注册ID 为空），运行时将无法执行",
                FallbackApplied = failurePolicyFallback
            };
        }

        var registry = await _dbContext.Set<CfAutoPluginRegistry>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FID == stage.F插件注册ID.Value, cancellationToken);
        if (registry == null)
        {
            return new StepFailureDto
            {
                Kind = "autoStageError",
                Message = $"插件注册不存在（ID={stage.F插件注册ID}），运行时将无法执行",
                FallbackApplied = failurePolicyFallback
            };
        }
        if (registry.F状态 != 1)
        {
            return new StepFailureDto
            {
                Kind = "autoStageError",
                Message = $"插件「{registry.F插件名称}」已禁用，运行时将无法执行",
                FallbackApplied = failurePolicyFallback
            };
        }

        if (stage.F插件规则ID.HasValue)
        {
            var rule = await _dbContext.Set<CfPluginRule>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.FID == stage.F插件规则ID.Value, cancellationToken);
            if (rule == null)
            {
                return new StepFailureDto
                {
                    Kind = "autoStageError",
                    Message = $"插件规则不存在（ID={stage.F插件规则ID}），运行时将无匹配规则",
                    FallbackApplied = failurePolicyFallback
                };
            }
            if (rule.F状态 != 1)
            {
                return new StepFailureDto
                {
                    Kind = "autoStageError",
                    Message = $"插件规则「{rule.F规则名称}」已禁用，运行时将无匹配规则",
                    FallbackApplied = failurePolicyFallback
                };
            }
        }

        // 配置完整，运行时是否成功依赖真实数据/插件执行，无法静态预判 → 不标注
        return null;
    }

    // 失败策略是否含兜底：stuckWithNotify=true 或 maxRetry>0 视为已配兜底（口径同运行时 FailurePolicy）
    private static bool HasFailureFallback(string? failurePolicyJson)
    {
        if (string.IsNullOrWhiteSpace(failurePolicyJson)) return false;
        try
        {
            using var document = JsonDocument.Parse(failurePolicyJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (document.RootElement.TryGetProperty("stuckWithNotify", out var stuck)
                && stuck.ValueKind == JsonValueKind.True)
                return true;
            if (document.RootElement.TryGetProperty("maxRetry", out var retry)
                && retry.ValueKind == JsonValueKind.Number
                && retry.TryGetInt32(out var maxRetry) && maxRetry > 0)
                return true;
            return false;
        }
        catch
        {
            return false;
        }
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

    // 样例卡片数据：InitialDataJson 打底 + DataJson 覆盖。路径预演与处理人干跑共用同一份，口径一致。
    private static Dictionary<string, object?> BuildCardData(CardFlowPathPreviewRequest request)
    {
        var cardData = ParseObject(request.InitialDataJson);
        foreach (var pair in ParseObject(request.DataJson))
        {
            cardData[pair.Key] = pair.Value;
        }
        return cardData;
    }

    // 人工节点处理人干跑：复用运行时 ApproverResolver，样例数据/发起人/组织驱动，失败不阻断预演
    private async Task<CardFlowPathPreviewApproverDto> ResolveApproverPreviewAsync(
        CfStageDefinition stage,
        IReadOnlyDictionary<string, object?> cardData,
        CfCard previewCard,
        CardFlowPathPreviewRequest request,
        string? flowSettingsJson,
        CancellationToken cancellationToken)
    {
        var dto = new CardFlowPathPreviewApproverDto { Strategy = stage.FAssigneeStrategy ?? string.Empty };
        if (string.IsNullOrWhiteSpace(stage.FAssigneeStrategy))
        {
            dto.Error = "未配置处理人策略";
            return dto;
        }
        try
        {
            var result = await _approverResolver.ResolveAsync(
                stage, previewCard, cardData, request.OrgId ?? 0, request.InitiatorId ?? 0, flowSettingsJson, cancellationToken);
            dto.ApproverNames = result.Approvers
                .OrderBy(a => a.SortOrder)
                .Select(a => string.IsNullOrWhiteSpace(a.UserName) ? $"#{a.UserId}" : a.UserName)
                .ToList();
            dto.FallbackReason = result.FallbackReason;
            dto.Error = result.ErrorMessage;
        }
        catch (Exception ex)
        {
            dto.Error = "处理人预演失败：" + ex.Message;
        }
        return dto;
    }

    private async Task<ConditionEvaluationContext> BuildPreviewContextAsync(
        CardFlowPathPreviewRequest request, Dictionary<string, object?> cardData, CancellationToken cancellationToken)
    {
        // 灌入明细样例数据，使引用 detailSummary.* 的条件边预演能真实求值（否则恒落默认分支，误导发布决策）
        var detailData = (request.Details ?? new List<PreviewDetailRow>())
            .OrderBy(row => row.SortOrder)
            .Select(row => (IReadOnlyDictionary<string, object?>)ParseObject(row.DataJson))
            .ToList();

        var context = ConditionContextFactory.Build(new ConditionContextInputs
        {
            CardData = cardData,
            DetailData = detailData,
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
