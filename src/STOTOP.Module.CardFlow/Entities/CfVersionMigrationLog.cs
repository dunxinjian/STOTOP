using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>版本迁移日志（CF版本迁移日志）：发布时 migrate 策略下，被删节点在途卡片的逐张迁移结果台账。</summary>
public class CfVersionMigrationLog : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FTenantId { get; set; }
    public long FOrgId { get; set; }
    public long FFlowDefinitionId { get; set; }
    public long FCardId { get; set; }
    public long FOldVersionId { get; set; }
    public long FNewVersionId { get; set; }
    public string? FOldStageKey { get; set; }
    public string? FNewStageKey { get; set; }
    /// <summary>migrated / skipped / failed</summary>
    public string FResult { get; set; } = string.Empty;
    public string? FMessage { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
