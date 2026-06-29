using STOTOP.Core.Models;

namespace STOTOP.Module.Salary.Entities;

/// <summary>
/// 薪酬档位
/// </summary>
public class SalaryGrade : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public string F档位编码 { get; set; } = string.Empty;
    public string F档位名称 { get; set; } = string.Empty;
    /// <summary>级别（数字越大级别越高）</summary>
    public int F级别 { get; set; }
    public decimal F基本工资 { get; set; }
    public decimal F岗位津贴 { get; set; }
    public decimal F绩效基数 { get; set; }
    public DateTime? F生效起期 { get; set; }
    public bool F启用状态 { get; set; } = true;
    public DateTime F创建时间 { get; set; } = DateTime.Now;
    public DateTime F更新时间 { get; set; } = DateTime.Now;
}
