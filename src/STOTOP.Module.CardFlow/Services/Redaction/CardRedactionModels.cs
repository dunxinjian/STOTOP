using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;

namespace STOTOP.Module.CardFlow.Services.Redaction;

/// <summary>脱敏请求：调用方（CardService）已从 DB 解析好查看者关系与节点配置。</summary>
public sealed class CardRedactionRequest
{
    public required CfCard Card { get; init; }
    public string? CardSchemaJson { get; init; }
    public string? DetailSchemaJson { get; init; }
    public IReadOnlyCollection<CfCardDetail> Details { get; init; } = Array.Empty<CfCardDetail>();

    /// <summary>查看者是当前 active 节点处理人时，传该节点配置（节点视图对其权威）。</summary>
    public StageConfigEnvelope? ActiveStageConfig { get; init; }

    /// <summary>查看者历史上处理过（非当前 active）的节点配置，用于粘附授权（提权保留）。</summary>
    public IReadOnlyCollection<StageConfigEnvelope> HandledStageConfigs { get; init; }
        = Array.Empty<StageConfigEnvelope>();

    /// <summary>草稿所有者编辑态：对所有字段给明文（仍走 allowlist 丢弃 schema 外键）。</summary>
    public bool OwnerEditMode { get; init; }
}

public sealed class ResolvedAccess
{
    public string Access { get; set; } = "readonly"; // editable|readonly|masked|hidden
    public string? MaskPattern { get; set; }
}

public sealed class RedactedDetailRowResult
{
    public long Id { get; set; }
    public int SortOrder { get; set; }
    public string DetailTableKey { get; set; } = "default";
    public string DataJson { get; set; } = "{}";
}

public sealed class CardRedactionResult
{
    public Dictionary<string, ResolvedAccess> FieldAccess { get; set; } = new();
    public Dictionary<string, ResolvedAccess> DetailAccess { get; set; } = new();
    public string RedactedDataJson { get; set; } = "{}";
    public string RedactedInitialDataJson { get; set; } = "{}";
    public List<RedactedDetailRowResult> RedactedDetails { get; set; } = new();
}
