using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Rules;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 路由条件命中率试算（设计 B4/E6）：取 definition 近 30 天卡片采样（上限 500 张），
/// 逐卡干跑 ConditionRuleEvaluator，回答"该条件在真实历史流量上的命中占比"。
/// 口径说明：OrgChain 按卡片组织现算（inOrgChain 可求值）；RoleCodes 不逐卡查询
/// （角色条件在试算中按无值处理，计入 withValue 缺口而非假命中）。
/// </summary>
public sealed class RouteHitEstimateService : IRouteHitEstimateService
{
    private const int SampleLimit = 500;
    private const int SampleDays = 30;

    private readonly STOTOPDbContext _dbContext;
    private readonly IConditionRuleEvaluator _conditionRuleEvaluator;

    public RouteHitEstimateService(STOTOPDbContext dbContext, IConditionRuleEvaluator conditionRuleEvaluator)
    {
        _dbContext = dbContext;
        _conditionRuleEvaluator = conditionRuleEvaluator;
    }

    public async Task<RouteHitEstimateDto> EstimateAsync(
        long definitionId,
        RouteHitEstimateRequest request,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.Now.AddDays(-SampleDays);
        var cards = await _dbContext.Set<CfCard>()
            .Where(card => card.FFlowDefinitionId == definitionId && card.FCreatedTime >= cutoff)
            .OrderByDescending(card => card.FCreatedTime)
            .Take(SampleLimit)
            .ToListAsync(cancellationToken);

        var result = new RouteHitEstimateDto { Total = cards.Count };
        if (cards.Count == 0)
            return result;

        var cardIds = cards.Select(card => card.FID).ToList();
        var detailsByCard = (await _dbContext.Set<CfCardDetail>()
                .Where(detail => cardIds.Contains(detail.FCardId))
                .ToListAsync(cancellationToken))
            .GroupBy(detail => detail.FCardId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(detail => detail.FSortOrder).ToList());

        var orgRows = (await _dbContext.Set<SysOrganization>()
                .Where(org => org.FStatus == 1)
                .Select(org => new { org.FID, org.FParentId })
                .ToListAsync(cancellationToken))
            .Select(org => (org.FID, org.FParentId))
            .ToList();

        // 有值探针按字段缓存：复用引擎 notEmpty 语义（HasMeaningfulValue + 路径解析），不另造一套判定
        var probeJsonByField = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in cards)
        {
            detailsByCard.TryGetValue(card.FID, out var details);
            var context = ConditionContextFactory.Build(new ConditionContextInputs
            {
                CardData = ParseObject(card.FDataJson),
                DetailData = (details ?? new List<CfCardDetail>())
                    .Select(detail => (IReadOnlyDictionary<string, object?>)ParseObject(detail.FDataJson))
                    .ToList(),
                SourceModule = card.FSourceModule,
                SourceType = card.FSourceType,
                SourceId = card.FSourceId,
                ReturnUrl = card.FReturnUrl,
                SourceTitle = card.FSourceTitle,
                InitiatorId = card.FInitiatorId,
                InitiatorName = card.FInitiatorName,
                OrgId = card.FOrgId,
                HasCurrentStage = false
            });
            context.OrgChain = OrgRoleContextResolver.BuildOrgChain(orgRows, card.FOrgId);

            var evaluation = _conditionRuleEvaluator.Evaluate(request.ConditionJson, context);
            if (evaluation.Matched)
                result.Hit++;

            if (AllConsumedFieldsHaveValue(evaluation.ConsumedFields, context, probeJsonByField))
                result.WithValue++;
        }

        return result;
    }

    private bool AllConsumedFieldsHaveValue(
        IReadOnlyList<string> consumedFields,
        ConditionEvaluationContext context,
        Dictionary<string, string> probeJsonByField)
    {
        foreach (var field in consumedFields)
        {
            if (!probeJsonByField.TryGetValue(field, out var probeJson))
            {
                probeJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["field"] = field,
                    ["operator"] = "notEmpty"
                });
                probeJsonByField[field] = probeJson;
            }

            if (!_conditionRuleEvaluator.Evaluate(probeJson, context).Matched)
                return false;
        }
        return true;
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
