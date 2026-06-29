using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 快递业务员名称映射（源脏名 → 员工工号）。
/// 用于把 STG/质量事件中的员工姓名原文等脏名归一到 EXP快递业务员.F员工编号。
/// </summary>
public class ExpSalesmanAlias : BaseEntity, IOrgScoped
{
    /// <summary>名称（源脏名：姓名/别名/混合文本）</summary>
    public string FName { get; set; } = string.Empty;
    /// <summary>员工工号（关联 EXP快递业务员.F工号 / ExpSalesman.FEmployeeNo）</summary>
    public string FEmployeeNo { get; set; } = string.Empty;
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
}
