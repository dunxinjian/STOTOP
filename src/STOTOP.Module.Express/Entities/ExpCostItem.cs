namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 成本项目（全局共享基础数据，不按组织隔离）
/// </summary>
public class ExpCostItem
{
    /// <summary>成本项目ID</summary>
    public int FID { get; set; }
    /// <summary>编码</summary>
    public string FCode { get; set; } = string.Empty;
    /// <summary>名称</summary>
    public string FName { get; set; } = string.Empty;
    /// <summary>是否返利</summary>
    public bool FIsRebate { get; set; } = false;
    /// <summary>排序</summary>
    public int FSortOrder { get; set; } = 0;
}
