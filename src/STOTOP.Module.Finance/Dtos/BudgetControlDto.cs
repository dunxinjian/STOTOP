namespace STOTOP.Module.Finance.Dtos;

public class BudgetPreviewRequest
{
    public long AccountSetId { get; set; }
    public long OrgId { get; set; }
    public string Period { get; set; } = string.Empty;
    public string SourceType { get; set; } = "cardflow_card";
    public long? SourceId { get; set; }
    public string? ExpenseType { get; set; }
    public string? AccountCode { get; set; }
    public long? PLItemId { get; set; }
    public decimal Amount { get; set; }
}

public class BudgetPreviewResult
{
    public bool MappingMissing { get; set; }
    public string? MissingReason { get; set; }
    public string? AccountCode { get; set; }
    public long? PLItemId { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal OccupiedAmount { get; set; }
    public decimal AvailableAmount { get; set; }
    public decimal RequestAmount { get; set; }
    public decimal GapAmount { get; set; }
    public string Policy { get; set; } = "warn";
    public bool Blocked { get; set; }
}
