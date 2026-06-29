using STOTOP.Module.CardFlow.Models.Rules;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class ConditionRuleEvaluatorTests
{
    [Fact]
    public void JsonRule_EvaluatesNestedAmountAndSourceConditions()
    {
        var evaluator = new ConditionRuleEvaluator();
        var context = new ConditionEvaluationContext
        {
            CardData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["amount"] = 6800m,
                ["expenseType"] = "travel"
            },
            SourceContext = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceType"] = "expenseReimburse"
            }
        };

        var result = evaluator.Evaluate(
            """
            {
              "logic":"and",
              "conditions":[
                {"field":"card.amount","operator":"gte","value":5000},
                {"field":"source.sourceType","operator":"eq","value":"expenseReimburse"},
                {"logic":"or","conditions":[
                  {"field":"card.expenseType","operator":"eq","value":"travel"},
                  {"field":"card.expenseType","operator":"eq","value":"office"}
                ]}
              ]
            }
            """,
            context);

        Assert.True(result.Matched);
        Assert.Empty(result.TypeErrors);
        Assert.Contains("card.amount", result.ConsumedFields);
        Assert.Contains("source.sourceType", result.ConsumedFields);
    }

    [Fact]
    public void JsonRule_FailsClosedOnTypeMismatch()
    {
        var evaluator = new ConditionRuleEvaluator();
        var context = new ConditionEvaluationContext
        {
            CardData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["amount"] = "not-a-number"
            }
        };

        var result = evaluator.Evaluate(
            """{"field":"card.amount","operator":"gte","value":5000}""",
            context);

        Assert.False(result.Matched);
        Assert.NotEmpty(result.TypeErrors);
    }

    [Fact]
    public void JsonRule_SupportsEmptyAndOrgChainOperators()
    {
        var evaluator = new ConditionRuleEvaluator();
        var context = new ConditionEvaluationContext
        {
            CardData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["loanId"] = null
            },
            OrgChain = new List<string> { "dept_01", "region_01", "hq_finance" }
        };

        var result = evaluator.Evaluate(
            """
            {
              "logic":"and",
              "conditions":[
                {"field":"card.loanId","operator":"empty"},
                {"field":"orgChain","operator":"inOrgChain","value":"hq_finance"}
              ]
            }
            """,
            context);

        Assert.True(result.Matched);
    }
}
