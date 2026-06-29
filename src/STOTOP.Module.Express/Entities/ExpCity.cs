namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 城市
/// </summary>
public class ExpCity
{
    /// <summary>城市ID</summary>
    public int FID { get; set; }
    /// <summary>行政区划编码</summary>
    public string FCode { get; set; } = string.Empty;
    /// <summary>城市名</summary>
    public string FName { get; set; } = string.Empty;
    /// <summary>关联省份ID</summary>
    public int FProvinceId { get; set; }
    /// <summary>省份名(冗余)</summary>
    public string FProvinceName { get; set; } = string.Empty;
}
