using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 快递网点名称映射（简称/全称/别名 → 网点编号）
/// </summary>
public class ExpNetworkPointAlias : BaseEntity, IOrgScoped
{
    /// <summary>名称（简称/全称/别名）</summary>
    public string FName { get; set; } = string.Empty;
    /// <summary>网点编号（关联 EXP快递网点.F编号）</summary>
    public string FNetworkPointCode { get; set; } = string.Empty;
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
}
