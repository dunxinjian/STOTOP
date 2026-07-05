using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 成本项目（按【租户=区域公司】隔离：各租户维护各自的成本项目字典 + F是否返利 标志，
/// 一个租户改动不再影响其它租户的成本符号）。计费侧按名称匹配取返利标志，故各租户的方案成本项
/// 名称需能在本租户字典命中；新租户上线须先种入其成本项目字典（否则返利标志缺失，由 WARN_COST_UNMATCHED_ITEM 告警）。
/// </summary>
public class ExpCostItem : ITenantScoped
{
    /// <summary>成本项目ID</summary>
    public int FID { get; set; }
    /// <summary>编码</summary>
    public string FCode { get; set; } = string.Empty;
    /// <summary>名称</summary>
    public string FName { get; set; } = string.Empty;
    /// <summary>是否返利</summary>
    public bool FIsRebate { get; set; } = false;
    /// <summary>排序</summary>
    public int FSortOrder { get; set; } = 0;
    /// <summary>租户ID（区域公司，多租户隔离键）</summary>
    public long FTenantId { get; set; }
}
