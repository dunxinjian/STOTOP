namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 省份
/// </summary>
public class ExpProvince
{
    /// <summary>省份ID</summary>
    public int FID { get; set; }
    /// <summary>编码</summary>
    public string FCode { get; set; } = string.Empty;
    /// <summary>名称</summary>
    public string FName { get; set; } = string.Empty;
    /// <summary>简称</summary>
    public string FShortName { get; set; } = string.Empty;
    /// <summary>大区</summary>
    public string? FRegion { get; set; }
    /// <summary>是否偏远</summary>
    public bool FIsRemote { get; set; }
}
