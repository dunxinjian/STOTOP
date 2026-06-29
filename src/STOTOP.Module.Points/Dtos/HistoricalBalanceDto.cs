namespace STOTOP.Module.Points.Dtos;

/// <summary>
/// 指定日期账户余额 DTO（清算 Job / 历史回查使用）
/// </summary>
public class HistoricalBalanceDto
{
    /// <summary>用户 ID</summary>
    public long UserId { get; set; }

    /// <summary>账户类型（1=A / 2=B）</summary>
    public int AccountType { get; set; }

    /// <summary>查询截止日期</summary>
    public DateTime AtDate { get; set; }

    /// <summary>账户余额</summary>
    public int Balance { get; set; }
}
