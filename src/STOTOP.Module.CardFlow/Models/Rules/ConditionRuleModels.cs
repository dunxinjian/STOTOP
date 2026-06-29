namespace STOTOP.Module.CardFlow.Models.Rules;

public sealed class ConditionRuleEvaluationResult
{
    public bool Matched { get; set; }
    public List<string> ConsumedFields { get; set; } = new();
    public List<string> TypeErrors { get; set; } = new();
    public string Explanation { get; set; } = string.Empty;

    public static ConditionRuleEvaluationResult Match(string explanation = "")
        => new() { Matched = true, Explanation = explanation };

    public static ConditionRuleEvaluationResult NoMatch(string explanation = "")
        => new() { Matched = false, Explanation = explanation };
}

public sealed class ConditionRuleItemResult
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public bool Matched { get; set; }
    public string Explanation { get; set; } = string.Empty;
}
