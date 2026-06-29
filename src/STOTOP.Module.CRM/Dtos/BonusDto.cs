namespace STOTOP.Module.CRM.Dtos;

public class BonusPlanDto
{
    public long Id { get; set; }
    public long? OrgId { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? CalcRules { get; set; }
    public int Status { get; set; }
    public long? OaProcessInstanceId { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? UpdaterName { get; set; }
    public DateTime? UpdatedTime { get; set; }
    public List<BonusDetailDto> Details { get; set; } = new();
}

public class CreateBonusPlanRequest
{
    public long? OrgId { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? CalcRules { get; set; }
    public List<CreateBonusDetailRequest>? Details { get; set; }
}

public class UpdateBonusPlanRequest
{
    public long? OrgId { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? CalcRules { get; set; }
    public List<CreateBonusDetailRequest>? Details { get; set; }
}

public class BonusPlanQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? OrgId { get; set; }
    public string? Period { get; set; }
    public int? Status { get; set; }
}

public class BonusDetailDto
{
    public long Id { get; set; }
    public long PlanId { get; set; }
    public long EmployeeId { get; set; }
    public decimal Amount { get; set; }
    public int BonusType { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class CreateBonusDetailRequest
{
    public long EmployeeId { get; set; }
    public decimal Amount { get; set; }
    public int BonusType { get; set; }
}

public class BonusDetailQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? PlanId { get; set; }
    public long? EmployeeId { get; set; }
    public long? OrgId { get; set; }
}
