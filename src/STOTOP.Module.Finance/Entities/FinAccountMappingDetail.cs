namespace STOTOP.Module.Finance.Entities;

public class FinAccountMappingDetail
{
    public Guid FID { get; set; }
    public Guid F方案ID { get; set; }
    public string F源科目编码 { get; set; } = string.Empty;
    public string? F源科目名称 { get; set; }
    public long? F目标科目ID { get; set; }
    public string F目标科目编码 { get; set; } = string.Empty;
    public string F目标科目名称 { get; set; } = string.Empty;
    public int F映射类型 { get; set; } = 1; // 1=直接 2=条件
    public string? F条件JSON { get; set; }
    public int F优先级 { get; set; } = 10;
    public string? F说明 { get; set; }
    public int F状态 { get; set; } = 1;
    public DateTime F创建时间 { get; set; }

    public FinMigrationScheme Scheme { get; set; } = null!;
}
