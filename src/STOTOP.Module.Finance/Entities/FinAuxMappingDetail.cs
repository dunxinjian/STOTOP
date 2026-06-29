namespace STOTOP.Module.Finance.Entities;

public class FinAuxMappingDetail
{
    public Guid FID { get; set; }
    public Guid F方案ID { get; set; }
    public string F辅助类型 { get; set; } = string.Empty; // customer/supplier/department/project/employee
    public string F源编码 { get; set; } = string.Empty;
    public string? F源名称 { get; set; }
    public long? F目标辅助项目ID { get; set; }
    public string? F目标编码 { get; set; }
    public string? F目标名称 { get; set; }
    public string? F处理策略 { get; set; } // null=跟方案/error/ignore/create
    public int F状态 { get; set; } = 1;
    public DateTime F创建时间 { get; set; }

    public FinMigrationScheme Scheme { get; set; } = null!;
}
