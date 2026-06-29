namespace STOTOP.Module.Vehicle.Dtos;

/// <summary>
/// 租赁收费记录详情 DTO
/// </summary>
public class RentalChargeDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public long VehicleId { get; set; }
    public long AssignmentId { get; set; }
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public long? RentalStandardId { get; set; }
    public DateTime ChargePeriodStart { get; set; }
    public DateTime ChargePeriodEnd { get; set; }
    public decimal AmountDue { get; set; }
    public decimal? AmountPaid { get; set; }
    public int ChargeStatus { get; set; }         // 1=待收, 2=已收, 3=逾期, 4=减免
    public DateTime? ChargeDate { get; set; }
    public long? VoucherId { get; set; }
    public string? Remark { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 租赁收费记录列表项 DTO
/// </summary>
public class RentalChargeListItemDto
{
    public long Id { get; set; }
    public long VehicleId { get; set; }
    public string? VehicleCode { get; set; }
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime ChargePeriodStart { get; set; }
    public DateTime ChargePeriodEnd { get; set; }
    public decimal AmountDue { get; set; }
    public decimal? AmountPaid { get; set; }
    public int ChargeStatus { get; set; }
    public DateTime? ChargeDate { get; set; }
}

/// <summary>
/// 批量生成租赁账单请求
/// </summary>
public class GenerateChargesRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
}

/// <summary>
/// 确认收费请求
/// </summary>
public class ConfirmChargeRequest
{
    public decimal AmountPaid { get; set; }
    public bool SyncToFinance { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 减免收费请求
/// </summary>
public class WaiveChargeRequest
{
    public string? Remark { get; set; }
}

/// <summary>
/// 租赁收费记录查询请求
/// </summary>
public class RentalChargeQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? VehicleId { get; set; }
    public long? EmployeeId { get; set; }
    public int? ChargeStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
