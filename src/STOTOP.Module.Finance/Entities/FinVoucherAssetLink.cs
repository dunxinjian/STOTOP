namespace STOTOP.Module.Finance.Entities;

public class FinVoucherAssetLink
{
    public Guid FID { get; set; }
    public long F凭证ID { get; set; }
    public long? F分录ID { get; set; }
    public long F资产卡片ID { get; set; }
    public int F关联类型 { get; set; } = 4; // 1=折旧 2=购置 3=处置 4=其他
    public long? F批次ID { get; set; }
    public DateTime F创建时间 { get; set; }
}
