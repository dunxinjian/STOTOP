using STOTOP.Core.Models;

namespace STOTOP.Module.Salary.Entities;

/// <summary>
/// 月度工资单
/// </summary>
public class SalaryPayroll : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long F员工ID { get; set; }
    /// <summary>期间，格式 yyyyMM</summary>
    public string F期间 { get; set; } = string.Empty;
    public decimal F基本工资 { get; set; }
    public decimal FKSF浮动 { get; set; }
    public decimal FPPV奖金 { get; set; }
    public decimal FB分兑换 { get; set; }
    public decimal F考勤扣减 { get; set; }
    public decimal F社保个人 { get; set; }
    public decimal F公积金个人 { get; set; }
    public decimal F个税 { get; set; }
    public decimal F应发合计 { get; set; }
    public decimal F实发合计 { get; set; }
    /// <summary>状态：0=草稿 1=待审 2=已审 3=已发放</summary>
    public int F状态 { get; set; }
    public long? F审核人ID { get; set; }
    public DateTime? F审核时间 { get; set; }
    public DateTime? F发放时间 { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
}
