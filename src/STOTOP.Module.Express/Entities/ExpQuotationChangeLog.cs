using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 快递报价_变更日志
/// </summary>
public class ExpQuotationChangeLog : BaseEntity
{
    /// <summary>报价ID</summary>
    public long FQuotationId { get; set; }
    /// <summary>变更时间</summary>
    public DateTime FChangeTime { get; set; } = DateTime.Now;
    /// <summary>变更人</summary>
    public string? FChangedBy { get; set; }
    /// <summary>变更类型 1价格修改 2店铺调整 3条款变更 4状态变更</summary>
    public int FChangeType { get; set; }
    /// <summary>变更前内容</summary>
    public string? FBeforeContent { get; set; }
    /// <summary>变更后内容</summary>
    public string? FAfterContent { get; set; }
    /// <summary>变更原因</summary>
    public string? FChangeReason { get; set; }
}
