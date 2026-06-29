using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 快递报价_共享别名
/// </summary>
public class ExpQuotationAlias : BaseEntity
{
    /// <summary>报价ID</summary>
    public long FQuotationId { get; set; }
    /// <summary>别名</summary>
    public string FAlias { get; set; } = string.Empty;
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;

    /// <summary>所属报价</summary>
    public ExpQuotation Quotation { get; set; } = null!;
}
