namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 快递店铺（主键为 FName / F名称）
/// </summary>
public class ExpShop
{
    /// <summary>名称（主键）</summary>
    public string FName { get; set; } = string.Empty;
    /// <summary>平台（淘宝/拼多多/抖音/得物等）</summary>
    public string? FPlatform { get; set; }
    /// <summary>是否共享</summary>
    public bool FIsShared { get; set; }
    /// <summary>是否自动创建</summary>
    public bool FIsAutoCreated { get; set; }
    /// <summary>待关联</summary>
    public bool FNeedsAssignment { get; set; }
    /// <summary>联系人</summary>
    public string? FContactName { get; set; }
    /// <summary>联系电话</summary>
    public string? FContactPhone { get; set; }
    /// <summary>状态 1启用 0停用</summary>
    public int FStatus { get; set; } = 1;
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

}
