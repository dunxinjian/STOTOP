using STOTOP.Module.CardFlow.Models.Rules;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class ConditionRuleEmptyGroupTests
{
    [Fact]
    public void EmptyConditionGroup_DoesNotMatch()
    {
        var result = new ConditionRuleEvaluator()
            .Evaluate("""{"logic":"and","conditions":[]}""", new ConditionEvaluationContext());
        Assert.False(result.Matched);
    }

    [Fact]
    public void OrGroup_ContainingOnlyEmptyGroup_DoesNotMatch()
    {
        var result = new ConditionRuleEvaluator()
            .Evaluate("""{"logic":"or","conditions":[{"logic":"and","conditions":[]}]}""", new ConditionEvaluationContext());
        Assert.False(result.Matched);
    }
}
