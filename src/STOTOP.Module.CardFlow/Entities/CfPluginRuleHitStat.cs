using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 插件规则命中统计（从 CfAutoPluginRuleHitStat 迁移而来）
/// </summary>
public class CfPluginRuleHitStat : BaseEntity, IOrgScoped
{
    /// <summary>规则ID</summary>
    public long FRuleId { get; set; }
    /// <summary>规则组序号</summary>
    public int FRuleGroupIndex { get; set; }
    /// <summary>分录行号</summary>
    public int FEntryLineNo { get; set; }
    /// <summary>批次ID（CfBatch.FID）</summary>
    public long FBatchId { get; set; }
    /// <summary>命中行数</summary>
    public int FHitRowCount { get; set; }
    /// <summary>未命中行数</summary>
    public int FMissRowCount { get; set; }
    /// <summary>统计时间</summary>
    public DateTime FStatTime { get; set; }
    /// <summary>是否已失效</summary>
    public bool FInvalidated { get; set; }
    public long FOrgId { get; set; }
}
