using STOTOP.Infrastructure.Data;

namespace STOTOP.Module.CardFlow.Services.Quality;

public interface IQualityRuleEngine
{
    /// <summary>加载指定暂存表/组织的规则（含全局规则）</summary>
    Task<List<QualityRuleDefinition>> LoadRulesAsync(string? targetTable, long orgId);

    /// <summary>对单行数据执行所有 Field/Row 级规则</summary>
    List<QualityViolation> ValidateRow(Dictionary<string, object?> row, int rowIndex, List<QualityRuleDefinition> rules);

    /// <summary>执行 Batch 级聚合规则（SQL方式批量检查）</summary>
    Task<List<QualityViolation>> ValidateBatchAsync(string targetTable, long batchId, List<QualityRuleDefinition> rules, STOTOPDbContext dbContext);
}

/// <summary>规则定义（从DB加载后的内存表示）</summary>
public class QualityRuleDefinition
{
    public long Id { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string RuleLevel { get; set; } = "Field";       // Field / Row / Batch
    public string CheckType { get; set; } = "NotNull";     // NotNull / Format / Range / Expression / SqlCondition
    public string? TargetField { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }  // 从 F规则参数JSON 解析
    public string ErrorTypeCode { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";      // Error / Warning
    public string? QualityDimension { get; set; }
    public string? MessageTemplate { get; set; }
    public string? SuggestedFix { get; set; }
    public bool IsBlocking { get; set; }
    public int Sort { get; set; }
}

/// <summary>规则违反记录</summary>
public class QualityViolation
{
    public long RuleId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string ErrorTypeCode { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string? QualityDimension { get; set; }
    public string? TargetField { get; set; }
    public string? OriginalValue { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? SuggestedFix { get; set; }
    public bool IsBlocking { get; set; }
    public int RowIndex { get; set; }           // 行号（Field/Row级）
    public long? StagingId { get; set; }        // 暂存表行ID（Batch级）
}
