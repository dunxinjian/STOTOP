using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 运单号段
/// </summary>
public class ExpWaybillNumberPool : BaseEntity, IOrgScoped
{
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    /// <summary>品牌ID</summary>
    public string FBrandCode { get; set; } = string.Empty;
    /// <summary>前缀</summary>
    public string? FPrefix { get; set; }
    /// <summary>起始号</summary>
    public string? FStartNo { get; set; }
    /// <summary>截止号</summary>
    public string? FEndNo { get; set; }
    /// <summary>总数量</summary>
    public int? FTotalCount { get; set; }
    /// <summary>已分配</summary>
    public int FAllocated { get; set; }
    // F剩余数量 is computed column (FTotalCount - FAllocated), read-only
    /// <summary>剩余数量（计算列）</summary>
    public int? FRemaining { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
