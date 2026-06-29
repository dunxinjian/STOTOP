namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 快递报价与加收项的关联表（复合主键）
/// </summary>
public class ExpQuotationSurchargeLink
{
    public long F报价ID { get; set; }
    public long F出港加收ID { get; set; }
    public DateTime F创建时间 { get; set; }
}
