namespace STOTOP.Module.Finance.Entities;

/// <summary>
/// 辅助核算别名（GUID主键，非BaseEntity）
/// </summary>
public class FinAuxiliaryAlias
{
    public Guid FID { get; set; } = Guid.NewGuid();
    public long F辅助核算项目ID { get; set; }
    public string F别名 { get; set; } = string.Empty;
    public string F辅助类型 { get; set; } = string.Empty;
    public Guid? F组织ID { get; set; }
}
