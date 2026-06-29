namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 礼金完整信息DTO
/// </summary>
public class GiftDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public long? AttendeeId { get; set; }
    public string? AttendeeName { get; set; }
    public string? GuestName { get; set; }  // 手动输入的宾客姓名
    public string? Camp { get; set; }
    public string? GuestType { get; set; }
    public decimal Amount { get; set; }
    public string? GiftDescription { get; set; }
    public DateTime RegistrationTime { get; set; }
    public string RegistrationMethod { get; set; } = "现金";
    public bool IsReturned { get; set; }
    public string? ReturnContent { get; set; }
    public DateTime? ReturnTime { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 创建礼金请求
/// </summary>
public class CreateGiftRequest
{
    public long? AttendeeId { get; set; }
    public string? GuestName { get; set; }  // 手动输入的宾客姓名
    public decimal Amount { get; set; }
    public string? GiftDescription { get; set; }
    public string RegistrationMethod { get; set; } = "现金";
    public string? Remark { get; set; }
}

/// <summary>
/// 更新礼金请求
/// </summary>
public class UpdateGiftRequest
{
    public decimal? Amount { get; set; }
    public string? GiftDescription { get; set; }
    public string? RegistrationMethod { get; set; }
    public bool? IsReturned { get; set; }
    public string? ReturnContent { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 批量登记礼金请求
/// </summary>
public class BatchRegisterGiftRequest
{
    public List<CreateGiftRequest> Items { get; set; } = new();
}

/// <summary>
/// 按阵营统计
/// </summary>
public class GiftCampSummary
{
    public string Camp { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// 礼金汇总DTO
/// </summary>
public class GiftSummaryDto
{
    public decimal TotalAmount { get; set; }
    public int TotalCount { get; set; }
    public int CashCount { get; set; }
    public int TransferCount { get; set; }
    public int GiftCount { get; set; }
    public int ReturnedCount { get; set; }
    public int PendingReturnCount { get; set; }
    public List<GiftCampSummary> CampSummaries { get; set; } = new();
}
