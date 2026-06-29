using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 质量规则（迁移自 CfQualityRule）
/// </summary>
public class CfQualityRule : BaseEntity
{
    /// <summary>规则名称</summary>
    public string FRuleName { get; set; } = string.Empty;
    /// <summary>规则编码</summary>
    public string FRuleCode { get; set; } = string.Empty;
    /// <summary>目标表名</summary>
    public string? FTargetTable { get; set; }
    /// <summary>规则级别：Field/Row/Batch</summary>
    public string FRuleLevel { get; set; } = "Field";
    /// <summary>检查类型：NotNull/Format/Range/Expression/SqlCondition</summary>
    public string FCheckType { get; set; } = "NotNull";
    /// <summary>目标字段</summary>
    public string? FTargetField { get; set; }
    /// <summary>规则参数 JSON</summary>
    public string? FRuleParamsJson { get; set; }
    /// <summary>错误类型编码</summary>
    public string FErrorCode { get; set; } = string.Empty;
    /// <summary>严重级别：Error/Warning</summary>
    public string FSeverityLevel { get; set; } = "Warning";
    /// <summary>质量维度：Completeness/Accuracy/Validity/Consistency</summary>
    public string? FQualityDimension { get; set; }
    /// <summary>错误消息模板</summary>
    public string? FErrorMessageTemplate { get; set; }
    /// <summary>建议修复方案</summary>
    public string? FSuggestedFix { get; set; }
    /// <summary>是否阻断后续 AutoPlugin 执行</summary>
    public bool FIsBlocking { get; set; }
    /// <summary>组织ID（0=全局）</summary>
    public long FOrgId { get; set; }
    /// <summary>排序</summary>
    public int FSortOrder { get; set; }
    /// <summary>是否启用</summary>
    public bool FEnabled { get; set; } = true;
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
