using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Models.Schema;

public sealed class CardPresentationResolveRequest
{
    public string? CardSchemaJson { get; set; }
    public string? DetailSchemaJson { get; set; }
    public StageViewProfile StageProfile { get; set; } = new();
    public Dictionary<string, StageFieldAccessRule> FieldAccess { get; set; } = new();
    public Dictionary<string, StageDetailAccessRule> DetailAccess { get; set; } = new();
    public CfCard Card { get; set; } = new();
    public IReadOnlyCollection<CfCardDetail> Details { get; set; } = Array.Empty<CfCardDetail>();
    public IReadOnlyCollection<CardPresentationRelation> Relations { get; set; } = Array.Empty<CardPresentationRelation>();
    public List<CardPresentationSnapshot> Snapshots { get; set; } = new();
}

public sealed class CardPresentationRuntimeView
{
    public List<CardComponentRuntimeDto> Components { get; set; } = new();
    public Dictionary<string, object?> DetailSummary { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CardComponentDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string Title { get; set; } = string.Empty;
    public CardComponentBinding Binding { get; set; } = new();
    public Dictionary<string, object?> Props { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public CardComponentValidationConfig? Validation { get; set; }
    public string? VisibilityCondition { get; set; }
    public string? Access { get; set; }
    public Dictionary<string, object?> Layout { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public CardComponentAggregationConfig? Aggregation { get; set; }
    public string? StatisticKey { get; set; }
}

public sealed class StageComponentRef
{
    public string Id { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Title { get; set; }
    public CardComponentBinding Binding { get; set; } = new();
    public Dictionary<string, object?> Props { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public CardComponentValidationConfig? Validation { get; set; }
    public string? VisibilityCondition { get; set; }
    public Dictionary<string, object?> Layout { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public CardComponentAggregationConfig? Aggregation { get; set; }
    public string? StatisticKey { get; set; }
}

public sealed class CardComponentBinding
{
    public string Source { get; set; } = "cardField";
    public string? FieldKey { get; set; }
    public string? DetailTableKey { get; set; }
    public string? SummaryKey { get; set; }
    public string? RelationType { get; set; }
    public string? SnapshotType { get; set; }
}

public sealed class CardComponentValidationConfig
{
    public int? MinRows { get; set; }
    public List<string> RequiredColumns { get; set; } = new();
}

public sealed class CardComponentAggregationConfig
{
    public List<CardComponentSumAggregation> Sum { get; set; } = new();
}

public sealed class CardComponentSumAggregation
{
    public string FieldKey { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
}

public sealed class StageComponentAccessRule
{
    public string Access { get; set; } = "readonly";
    public bool? Required { get; set; }
    public string? MaskPattern { get; set; }
}

public sealed class CardComponentRuntimeDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string Title { get; set; } = string.Empty;
    public string Access { get; set; } = "readonly";
    public bool Visible { get; set; } = true;
    public bool Editable { get; set; }
    public bool Required { get; set; }
    public bool Masked { get; set; }
    public CardComponentBinding Binding { get; set; } = new();
    public Dictionary<string, object?> Props { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public object? Value { get; set; }
    public string? StatisticKey { get; set; }
    public List<CardComponentColumnRuntimeDto> Columns { get; set; } = new();
    public List<CardComponentRowRuntimeDto> Rows { get; set; } = new();
    public List<CardPresentationSnapshot> Snapshots { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class CardComponentColumnRuntimeDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string Access { get; set; } = "readonly";
    public bool Editable { get; set; }
    public bool Required { get; set; }
    public bool Masked { get; set; }
}

public sealed class CardComponentRowRuntimeDto
{
    public long? Id { get; set; }
    public int SortOrder { get; set; }
    public Dictionary<string, object?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CardPresentationSnapshot
{
    public string SnapshotType { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Dictionary<string, object?> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CardPresentationRelation
{
    public long Id { get; set; }
    public long SourceCardId { get; set; }
    public string? SourceCardNumber { get; set; }
    public string? SourceCardTitle { get; set; }
    public long TargetCardId { get; set; }
    public string? TargetCardNumber { get; set; }
    public string? TargetCardTitle { get; set; }
    public string RelationType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? OffsetAmount { get; set; }
    public Dictionary<string, object?> SnapshotData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
