using STOTOP.Core.Models;

namespace STOTOP.Module.Salary.Entities;

/// <summary>
/// 员工薪酬档案
/// </summary>
public class SalaryArchive : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long F员工ID { get; set; }
    /// <summary>关联 SalaryGrade</summary>
    public long F档位ID { get; set; }
    public DateTime F入档日期 { get; set; }
    public decimal F基本工资 { get; set; }
    public decimal F岗位津贴 { get; set; }
    public decimal F社保基数 { get; set; }
    public decimal F公积金基数 { get; set; }
    /// <summary>个税起征额，默认 5000</summary>
    public decimal F个税起征额 { get; set; } = 5000m;
    public string? F备注 { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
    public DateTime F更新时间 { get; set; } = DateTime.Now;
}
