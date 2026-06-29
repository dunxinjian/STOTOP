using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 快递报价_关联店铺
/// </summary>
public class ExpQuotationShop : BaseEntity
{
    /// <summary>报价ID</summary>
    public long FQuotationId { get; set; }
    /// <summary>店铺名称</summary>
    public string FShopName { get; set; } = string.Empty;
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;

    /// <summary>所属报价方案</summary>
    public ExpQuotation Quotation { get; set; } = null!;
}
