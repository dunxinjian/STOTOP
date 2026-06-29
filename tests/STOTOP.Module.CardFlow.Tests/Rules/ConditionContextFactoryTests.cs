using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class ConditionContextFactoryTests
{
    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] pairs)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in pairs)
            dict[key] = value;
        return dict;
    }

    [Fact]
    public void Build_InitiatorOrg_UsesIdKeyNotOrgId()
    {
        var context = ConditionContextFactory.Build(new ConditionContextInputs
        {
            CardData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            OrgId = 42
        });

        Assert.True(context.InitiatorOrg.ContainsKey("id"));
        Assert.False(context.InitiatorOrg.ContainsKey("orgId"));
        Assert.Equal(42L, context.InitiatorOrg["id"]);
    }

    [Fact]
    public void Build_EmptyDetails_DetailSummaryAllZeros()
    {
        var context = ConditionContextFactory.Build(new ConditionContextInputs
        {
            CardData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        });

        Assert.Equal(0, context.DetailSummary["rowCount"]);
        Assert.Equal(0m, context.DetailSummary["amount"]);
        Assert.Equal(0m, context.DetailSummary["tax"]);
        Assert.Equal(0m, context.DetailSummary["actualPayAmount"]);
    }

    [Fact]
    public void Build_Details_AggregatesAmountAndFallsBackToAmountWhenNoPayField()
    {
        var context = ConditionContextFactory.Build(new ConditionContextInputs
        {
            CardData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            DetailData = new[]
            {
                (IReadOnlyDictionary<string, object?>)Row(("amount", 100m), ("tax", 6m)),
                (IReadOnlyDictionary<string, object?>)Row(("amount", 200m))
            }
        });

        Assert.Equal(2, context.DetailSummary["rowCount"]);
        Assert.Equal(300m, context.DetailSummary["amount"]);
        Assert.Equal(6m, context.DetailSummary["tax"]);
        Assert.Equal(300m, context.DetailSummary["actualPayAmount"]);
    }

    [Fact]
    public void Build_Details_UsesPayFieldsWhenPresent()
    {
        var context = ConditionContextFactory.Build(new ConditionContextInputs
        {
            CardData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            DetailData = new[]
            {
                (IReadOnlyDictionary<string, object?>)Row(("amount", 100m), ("payAmount", 40m)),
                (IReadOnlyDictionary<string, object?>)Row(("amount", 200m), ("实付金额", 10m))
            }
        });

        Assert.Equal(50m, context.DetailSummary["actualPayAmount"]);
    }

    [Fact]
    public void Build_HasCurrentStageFalse_CurrentStageResultEmpty()
    {
        var context = ConditionContextFactory.Build(new ConditionContextInputs
        {
            CardData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            HasCurrentStage = false
        });

        Assert.Empty(context.CurrentStageResult);
    }

    [Fact]
    public void Build_HasCurrentStageTrue_PopulatesRoundStageAction()
    {
        var context = ConditionContextFactory.Build(new ConditionContextInputs
        {
            CardData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            HasCurrentStage = true,
            CurrentRound = 2,
            CurrentStageInstanceId = 7,
            CurrentStageAction = "approve"
        });

        Assert.Equal(2, context.CurrentStageResult["round"]);
        Assert.Equal(7L, context.CurrentStageResult["stageInstanceId"]);
        Assert.Equal("approve", context.CurrentStageResult["action"]);
    }
}
