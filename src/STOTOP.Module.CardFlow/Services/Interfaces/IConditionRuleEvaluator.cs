using STOTOP.Module.CardFlow.Models.Rules;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IConditionRuleEvaluator
{
    ConditionRuleEvaluationResult Evaluate(string? conditionJson, ConditionEvaluationContext context);
}
