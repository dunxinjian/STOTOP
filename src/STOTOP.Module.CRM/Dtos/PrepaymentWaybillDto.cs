namespace STOTOP.Module.CRM.Dtos;

#region WaybillPool

public class WaybillPoolDto
{
    public long Id { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string? Prefix { get; set; }
    public string StartNo { get; set; } = string.Empty;
    public string EndNo { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int AllocatedCount { get; set; }
    public int RemainingCount { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public decimal UnitPrice { get; set; }
    public int Status { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class CreateWaybillPoolRequest
{
    public string BrandCode { get; set; } = string.Empty;
    public string? Prefix { get; set; }
    public string StartNo { get; set; } = string.Empty;
    public string EndNo { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public decimal UnitPrice { get; set; }
}

public class WaybillPoolQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? BrandCode { get; set; }
    public int? Status { get; set; }
}

#endregion

#region CustomerAccount

public class CustomerAccountDto
{
    public long Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string BrandCode { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal TotalRecharge { get; set; }
    public decimal TotalConsumption { get; set; }
    public decimal FrozenAmount { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

#endregion

#region Prepayment

public class PrepaymentDto
{
    public long Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public long CustomerAccountId { get; set; }
    public long? OrgId { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public decimal PrepayAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public int ExpectedWaybillCount { get; set; }
    public int AllocatedWaybillCount { get; set; }
    public int Status { get; set; }
    public long? BankTransactionId { get; set; }
    public string? Remark { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class CreatePrepaymentRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public long CustomerAccountId { get; set; }
    public long? OrgId { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public decimal PrepayAmount { get; set; }
    public int ExpectedWaybillCount { get; set; }
    public string? Remark { get; set; }
}

public class PrepaymentQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? CustomerId { get; set; }
    public string? BrandCode { get; set; }
    public int? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

#endregion

#region WaybillAllocation

public class WaybillAllocationDto
{
    public long Id { get; set; }
    public long PrepaymentId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public long PoolId { get; set; }
    public string StartNo { get; set; } = string.Empty;
    public string EndNo { get; set; } = string.Empty;
    public int AllocatedCount { get; set; }
    public DateOnly AllocationDate { get; set; }
    public long OperatorId { get; set; }
    public int Status { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class AllocateWaybillRequest
{
    public long PrepaymentId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public long PoolId { get; set; }
    public int Count { get; set; }
    public long OperatorId { get; set; }
}

#endregion
