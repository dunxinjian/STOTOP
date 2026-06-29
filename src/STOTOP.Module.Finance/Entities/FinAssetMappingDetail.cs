namespace STOTOP.Module.Finance.Entities;

public class FinAssetMappingDetail
{
    public Guid FID { get; set; }
    public Guid F方案ID { get; set; }
    public string F源资产编号 { get; set; } = string.Empty;
    public long? F目标资产卡片ID { get; set; }
    public string? F目标资产编号 { get; set; }
    public string? F目标资产名称 { get; set; }
    public int F状态 { get; set; } = 1;
    public DateTime F创建时间 { get; set; }

    public FinMigrationScheme Scheme { get; set; } = null!;
}
