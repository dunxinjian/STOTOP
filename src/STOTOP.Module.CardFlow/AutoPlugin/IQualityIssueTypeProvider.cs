namespace STOTOP.Module.CardFlow.AutoPlugin;

/// <summary>
/// 质量问题类型定义提供者 - 各 AutoPlugin 实现此接口注册自己的问题类型
/// 迁移自 CFAutoPlugin，原迁移自 DataCenter
/// </summary>
public interface IQualityIssueTypeProvider
{
    IEnumerable<QualityIssueTypeDefinition> GetIssueTypeDefinitions();
}

/// <summary>质量问题类型定义（元数据）</summary>
public record QualityIssueTypeDefinition(
    string Code,
    string Name,
    string Module,
    string Category,
    string SeverityLevel,
    string DetailRoute,
    string? SourceAutoPlugin = null,
    string? Description = null,
    string? SuggestedFix = null
);
