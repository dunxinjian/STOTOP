using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 检测规则
/// </summary>
public class QlRule : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public string FRuleName { get; set; } = string.Empty;
    public string FBusinessLine { get; set; } = string.Empty;
    public string? FConditionExpression { get; set; }
    public int FDispatchMethod { get; set; }
    public string? FDispatchTarget { get; set; }
    public int FDefaultPriority { get; set; }
    public int FTimeoutHours { get; set; } = 24;
    public int FStatus { get; set; } = 1;
    public string? FDescription { get; set; }
    public long FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime? FUpdatedTime { get; set; }
}
