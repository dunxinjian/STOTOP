namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 快递品牌（主键为 F编码 NCHAR(2)，全局共享基础数据，不按组织隔离）
/// </summary>
public class ExpBrand
{
    /// <summary>编码</summary>
    public string FCode { get; set; } = string.Empty;
    /// <summary>名称</summary>
    public string FName { get; set; } = string.Empty;
    /// <summary>状态 1启用 0停用</summary>
    public int FStatus { get; set; } = 1;
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
