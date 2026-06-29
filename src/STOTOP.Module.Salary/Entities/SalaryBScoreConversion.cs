using STOTOP.Core.Models;

namespace STOTOP.Module.Salary.Entities;

/// <summary>
/// B 分兑换记录
/// </summary>
public class SalaryBScoreConversion : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long F员工ID { get; set; }
    /// <summary>期间，格式 yyyyMM</summary>
    public string F期间 { get; set; } = string.Empty;
    /// <summary>清零前余额</summary>
    public int FB分余额 { get; set; }
    public decimal F兑换比例 { get; set; }
    public decimal F兑换金额 { get; set; }
    /// <summary>兑换类型：1=福利券 2=工资</summary>
    public int F兑换类型 { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
}
