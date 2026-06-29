namespace STOTOP.Module.CardFlow.Models;

/// <summary>凭证分录规则（内存 DTO，从 DCAgent规则 JSON 转换而来）</summary>
public class VoucherEntryRule
{
    public string RuleGroupName { get; set; } = string.Empty;
    public int LineNo { get; set; }
    public string Direction { get; set; } = string.Empty;
    public long? AccountId { get; set; }
    public string? AccountMatchField { get; set; }
    public string? AccountMatchRulesJson { get; set; }
    public string AmountField { get; set; } = string.Empty;
    public string AmountAggregation { get; set; } = "SUM";
    public string? SummaryTemplate { get; set; }
    public string? ConditionField { get; set; }
    public string? ConditionValuesJson { get; set; }
    public string? AuxiliaryConfigJson { get; set; }
    public int Priority { get; set; } = 100;
    public int Status { get; set; } = 1;
    public long AccountSetId { get; set; }
}
