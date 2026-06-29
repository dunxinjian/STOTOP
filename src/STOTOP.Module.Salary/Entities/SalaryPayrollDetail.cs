using STOTOP.Core.Models;

namespace STOTOP.Module.Salary.Entities;

/// <summary>
/// 工资明细
/// </summary>
public class SalaryPayrollDetail : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    /// <summary>外键→SalaryPayroll</summary>
    public long F工资单ID { get; set; }
    /// <summary>项目类型：1=基本工资...11=其他补贴</summary>
    public int F项目类型 { get; set; }
    public string F项目名称 { get; set; } = string.Empty;
    public decimal F金额 { get; set; }
    /// <summary>来源记录ID</summary>
    public long? F来源ID { get; set; }
    public string? F来源类型 { get; set; }
    public string? F备注 { get; set; }
}
