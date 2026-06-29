using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 质量问题类型注册表（迁移自 CfQualityIssueType）
/// 定义系统支持的所有质量问题类型及其派发配置
/// </summary>
public class CfQualityIssueType : BaseEntity
{
    public string FCode { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public string FModule { get; set; } = "Express";
    public string? FSourceAutoPlugin { get; set; }
    public string FSeverityLevel { get; set; } = "Warning";
    public string FCategory { get; set; } = "DataQuality";
    public bool FIsBuiltIn { get; set; }
    public string? FSuggestedFix { get; set; }
    public string? FDetailRoute { get; set; }
    public string? FDispatchMode { get; set; }
    public string? FDispatchTarget { get; set; }
    public string? FResolveMode { get; set; }
    public string? FCardFlowCode { get; set; }
    public string? FCardTemplateCode { get; set; }
    public string? FActionSchemaJson { get; set; }
    public string? FAfterResolvedAction { get; set; }
    public string? FAggregationMode { get; set; }
    public bool FOrgScoped { get; set; } = true;
    public int FTimeoutHours { get; set; }
    public int FStatus { get; set; } = 1;
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
