using STOTOP.Module.CardFlow.Models.Rules;

namespace STOTOP.Module.CardFlow.Services;

public sealed class ConditionContextInputs
{
    public required Dictionary<string, object?> CardData { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> DetailData { get; init; }
        = Array.Empty<IReadOnlyDictionary<string, object?>>();

    public string? SourceModule { get; init; }
    public string? SourceType { get; init; }
    public long? SourceId { get; init; }
    public string? ReturnUrl { get; init; }
    public string? SourceTitle { get; init; }

    public long? InitiatorId { get; init; }
    public string? InitiatorName { get; init; }

    public long? OrgId { get; init; }

    public int? CurrentRound { get; init; }
    public long? CurrentStageInstanceId { get; init; }
    public string? CurrentStageAction { get; init; }
    public bool HasCurrentStage { get; init; }
}

public static class ConditionContextFactory
{
    public static ConditionEvaluationContext Build(ConditionContextInputs inputs)
    {
        var context = new ConditionEvaluationContext
        {
            CardData = inputs.CardData,
            SourceContext = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceModule"] = inputs.SourceModule,
                ["sourceType"] = inputs.SourceType,
                ["sourceId"] = inputs.SourceId,
                ["returnUrl"] = inputs.ReturnUrl,
                ["sourceTitle"] = inputs.SourceTitle
            },
            Initiator = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = inputs.InitiatorId,
                ["name"] = inputs.InitiatorName
            },
            InitiatorOrg = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = inputs.OrgId
            },
            DetailSummary = BuildDetailSummary(inputs.DetailData)
        };

        if (inputs.HasCurrentStage)
        {
            context.CurrentStageResult = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["round"] = inputs.CurrentRound,
                ["stageInstanceId"] = inputs.CurrentStageInstanceId,
                ["action"] = inputs.CurrentStageAction
            };
        }

        return context;
    }

    private static Dictionary<string, object?> BuildDetailSummary(
        IEnumerable<IReadOnlyDictionary<string, object?>> details)
    {
        var rowCount = 0;
        var amount = 0m;
        var tax = 0m;
        var actualPayAmount = 0m;
        foreach (var data in details)
        {
            rowCount++;
            amount += ReadDecimal(data, "amount");
            tax += ReadDecimal(data, "tax");
            actualPayAmount += ReadDecimal(data, "actualPayAmount")
                + ReadDecimal(data, "payAmount")
                + ReadDecimal(data, "实付金额");
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["rowCount"] = rowCount,
            ["amount"] = amount,
            ["tax"] = tax,
            ["actualPayAmount"] = actualPayAmount == 0m ? amount : actualPayAmount
        };
    }

    private static decimal ReadDecimal(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value == null)
            return 0m;
        return decimal.TryParse(value.ToString(), out var result) ? result : 0m;
    }
}
