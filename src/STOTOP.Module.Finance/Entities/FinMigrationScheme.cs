namespace STOTOP.Module.Finance.Entities;

public class FinMigrationScheme
{
    public Guid FID { get; set; }
    public string F方案名称 { get; set; } = string.Empty;
    public string F源账套标识 { get; set; } = string.Empty;
    public long F目标账套ID { get; set; }
    public string F辅助项缺失策略 { get; set; } = "error"; // error/ignore/create
    public string? F说明 { get; set; }
    public int F状态 { get; set; } = 1;
    public long F组织ID { get; set; }
    public DateTime F创建时间 { get; set; }
    public DateTime F更新时间 { get; set; }

    // Navigation
    public List<FinAccountMappingDetail> AccountMappings { get; set; } = new();
    public List<FinAuxMappingDetail> AuxMappings { get; set; } = new();
    public List<FinAssetMappingDetail> AssetMappings { get; set; } = new();
}
