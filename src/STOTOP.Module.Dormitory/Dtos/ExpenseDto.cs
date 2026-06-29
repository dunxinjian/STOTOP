namespace STOTOP.Module.Dormitory.Dtos;

/// <summary>
/// 费用记录详情 DTO
/// </summary>
public class ExpenseDto
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string ExpenseType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Month { get; set; } = string.Empty;
    public string? ShareMethod { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 费用记录列表项 DTO
/// </summary>
public class ExpenseListItemDto
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string ExpenseType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Month { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建费用记录请求
/// </summary>
public class CreateExpenseRequest
{
    public long RoomId { get; set; }
    public string ExpenseType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Month { get; set; } = string.Empty;
    public string? ShareMethod { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新费用记录请求
/// </summary>
public class UpdateExpenseRequest
{
    public string ExpenseType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Month { get; set; } = string.Empty;
    public string? ShareMethod { get; set; }
    public string? Remark { get; set; }
    /// <summary>缴费状态（0=待缴/1=已缴/2=减免），不传则保持原值</summary>
    public int? Status { get; set; }
}

/// <summary>
/// 费用记录查询请求
/// </summary>
public class ExpenseQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? BuildingId { get; set; }
    public long? RoomId { get; set; }
    public string? Month { get; set; }
    public string? ExpenseType { get; set; }
}

/// <summary>
/// 月度费用汇总 DTO
/// </summary>
public class MonthlyExpenseSummaryDto
{
    public string Month { get; set; } = string.Empty;
    public string ExpenseType { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// 费用分摊结果 DTO（按房间当前在住人分摊）
/// </summary>
public class ExpenseAllocationDto
{
    public long ExpenseId { get; set; }
    public long RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    /// <summary>分摊方式：Equal=均摊（总额按人数等分）/ Fixed=固定（每人按金额收取）</summary>
    public string? ShareMethod { get; set; }
    public decimal ExpenseAmount { get; set; }
    public int OccupantCount { get; set; }
    /// <summary>实际分摊合计（均摊时严格等于费用总额；固定时为金额×人数）</summary>
    public decimal AllocatedTotal { get; set; }
    public List<ExpenseShareDto> Shares { get; set; } = new();
}

/// <summary>
/// 个人费用分摊项
/// </summary>
public class ExpenseShareDto
{
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public decimal Amount { get; set; }
}
