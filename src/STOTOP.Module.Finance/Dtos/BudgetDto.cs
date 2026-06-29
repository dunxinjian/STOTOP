namespace STOTOP.Module.Finance.Dtos;

public class BudgetVersionDto
{
    public long Id { get; set; }
    public long AccountSetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ScenarioType { get; set; } = "annual_budget";
    public int Year { get; set; }
    public string Status { get; set; } = "draft";
    public long OwnerOrgId { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedTime { get; set; }
}

public class CreateBudgetVersionRequest
{
    public long AccountSetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ScenarioType { get; set; } = "annual_budget";
    public int Year { get; set; }
    public long OwnerOrgId { get; set; }
}

public class BudgetLineDto
{
    public long Id { get; set; }
    public long BudgetVersionId { get; set; }
    public string Period { get; set; } = string.Empty;
    public long OrgId { get; set; }
    public long? AmoebaUnitId { get; set; }
    public long? AccountId { get; set; }
    public string? AccountCode { get; set; }
    public long? PLItemId { get; set; }
    public string? DimensionJson { get; set; }
    public decimal Amount { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Remark { get; set; }
}

public class BatchUpsertBudgetLinesRequest
{
    public List<BudgetLineDto> Lines { get; set; } = new();
}

public class BudgetExpenseMappingDto
{
    public long Id { get; set; }
    public long AccountSetId { get; set; }
    public long? OrgId { get; set; }
    public string ExpenseType { get; set; } = string.Empty;
    public string? AccountCode { get; set; }
    public long? PLItemId { get; set; }
    public string CashCategory { get; set; } = "expense_reimbursement";
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}
