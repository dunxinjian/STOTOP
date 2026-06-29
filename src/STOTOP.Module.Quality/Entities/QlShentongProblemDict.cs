using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 申通质量问题字典：把各 STG 来源的问题类型原文归一为统一的问题类型编码/名称、严重度、是否考核、是否可归责。
/// 表：QL申通_质量问题字典。FID 由 DbContext 统一配 IDENTITY 主键，实体不声明。
/// </summary>
public class QlShentongProblemDict : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    /// <summary>承运商（固定"申通"）</summary>
    public string F承运商 { get; set; } = "申通";
    /// <summary>质量域（如 物流完整性/派送/签收/投诉/遗失 等）</summary>
    public string F质量域 { get; set; } = string.Empty;
    /// <summary>来源问题类型原文（STG 表中的原始问题类型文本）</summary>
    public string F来源问题类型原文 { get; set; } = string.Empty;
    /// <summary>归一后的问题类型编码</summary>
    public string F问题类型编码 { get; set; } = string.Empty;
    /// <summary>归一后的问题类型名称</summary>
    public string F问题类型名称 { get; set; } = string.Empty;
    /// <summary>问题大类</summary>
    public string? F问题大类 { get; set; }
    /// <summary>问题小类</summary>
    public string? F问题小类 { get; set; }
    /// <summary>默认严重度</summary>
    public int F默认严重度 { get; set; }
    /// <summary>是否考核</summary>
    public bool F是否考核 { get; set; }
    /// <summary>是否可归责到人</summary>
    public bool F是否可归责到人 { get; set; }
    /// <summary>默认整改时限（小时）</summary>
    public int? F默认整改时限小时 { get; set; }
    /// <summary>状态（1=启用）</summary>
    public int F状态 { get; set; } = 1;
    /// <summary>备注</summary>
    public string? F备注 { get; set; }
}
