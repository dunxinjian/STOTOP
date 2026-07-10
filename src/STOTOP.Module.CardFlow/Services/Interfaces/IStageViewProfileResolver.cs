using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Models.Schema;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IStageViewProfileResolver
{
    StageViewResolutionResult Resolve(
        string? cardSchemaJson,
        string? detailSchemaJson,
        CfStageDefinition stageDefinition,
        CfCard card,
        IReadOnlyCollection<CfCardDetail> details,
        long operatorId,
        StageConfigEnvelope normalizedConfig,
        IReadOnlyCollection<CardPresentationRelation>? relations = null,
        IReadOnlyCollection<CardPresentationSnapshot>? snapshots = null);
}

public sealed class StageViewResolutionResult
{
    public List<StageViewSection> Sections { get; set; } = new();
    public Dictionary<string, StageFieldAccessRule> FieldAccess { get; set; } = new();
    public Dictionary<string, StageDetailAccessRule> DetailAccess { get; set; } = new();
    public StageSummaryProfile? Summary { get; set; }
    public List<string> AllowedActions { get; set; } = new();
    /// <summary>设计器自定义动作按钮（M8-C），透传给前端动态渲染。</summary>
    public List<CustomActionDefinition> CustomActions { get; set; } = new();
    public CardPresentationRuntimeView Presentation { get; set; } = new();
    public string RedactedDataJson { get; set; } = "{}";
    public List<RedactedDetailRow> RedactedDetails { get; set; } = new();
}

public sealed class RedactedDetailRow
{
    public long Id { get; set; }
    public int SortOrder { get; set; }
    public string DetailTableKey { get; set; } = "default";
    public string DataJson { get; set; } = "{}";
}
