using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Rules;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public sealed class ConditionEvaluationContextBuilder : IConditionEvaluationContextBuilder
{
    private readonly STOTOPDbContext _dbContext;

    public ConditionEvaluationContextBuilder(STOTOPDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ConditionEvaluationContext> BuildAsync(
        CfCard card,
        CfStageInstance? currentStage = null,
        CancellationToken cancellationToken = default)
    {
        var details = await _dbContext.Set<CfCardDetail>()
            .Where(detail => detail.FCardId == card.FID)
            .ToListAsync(cancellationToken);

        var detailData = details
            .Select(detail => (IReadOnlyDictionary<string, object?>)ParseObject(detail.FDataJson))
            .ToList();

        var context = ConditionContextFactory.Build(new ConditionContextInputs
        {
            CardData = ParseObject(card.FDataJson),
            DetailData = detailData,
            SourceModule = card.FSourceModule,
            SourceType = card.FSourceType,
            SourceId = card.FSourceId,
            ReturnUrl = card.FReturnUrl,
            SourceTitle = card.FSourceTitle,
            InitiatorId = card.FInitiatorId,
            InitiatorName = card.FInitiatorName,
            OrgId = card.FOrgId,
            CurrentRound = card.FCurrentRound,
            CurrentStageInstanceId = currentStage?.FID,
            CurrentStageAction = currentStage?.FFinalAction,
            HasCurrentStage = true
        });

        if (currentStage?.FStageDefinitionId != null)
        {
            var stageKey = await _dbContext.Set<CfStageDefinition>()
                .Where(stage => stage.FID == currentStage.FStageDefinitionId.Value)
                .Select(stage => stage.FStageKey)
                .FirstOrDefaultAsync(cancellationToken);
            context.CurrentStageResult["completedStageKey"] = stageKey;
        }

        var orgRole = await OrgRoleContextResolver.ResolveAsync(
            _dbContext, card.FOrgId, card.FInitiatorId, cancellationToken);
        context.OrgChain = orgRole.OrgChain;
        context.RoleCodes = orgRole.RoleCodes;
        context.RoleNames = orgRole.RoleNames;

        return context;
    }

    private static Dictionary<string, object?> ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.EnumerateObject().ToDictionary(
                    property => property.Name,
                    property => ToPlainValue(property.Value),
                    StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static object? ToPlainValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ToPlainValue).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToPlainValue(p.Value), StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
    }
}
