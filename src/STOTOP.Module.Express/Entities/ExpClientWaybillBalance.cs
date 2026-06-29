using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 客户运单号余额
/// </summary>
public class ExpClientWaybillBalance : BaseEntity, IOrgScoped
{
    /// <summary>业务对象ID（F编号）</summary>
    public string FBusinessObjectId { get; set; } = string.Empty;
    /// <summary>品牌ID</summary>
    public string FBrandCode { get; set; } = string.Empty;
    /// <summary>可用数量</summary>
    public int FAvailable { get; set; }
    /// <summary>已用数量</summary>
    public int FUsed { get; set; }
    /// <summary>累计分配</summary>
    public int FTotalAllocated { get; set; }
    /// <summary>累计回收</summary>
    public int FTotalReturned { get; set; }
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
