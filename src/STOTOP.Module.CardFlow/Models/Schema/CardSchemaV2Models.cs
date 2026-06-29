namespace STOTOP.Module.CardFlow.Models.Schema;

public sealed class CardSchemaV2
{
    public int Version { get; set; } = 2;
    public List<CardFieldDefinitionV2> Fields { get; set; } = new();
    public List<CardComponentDefinition> Components { get; set; } = new();
}

public sealed class CardDetailSchemaV2
{
    public int Version { get; set; } = 2;
    public List<CardDetailTableDefinitionV2> Tables { get; set; } = new();
}

public sealed class CardDetailTableDefinitionV2
{
    public string DetailTableKey { get; set; } = "default";
    public string Label { get; set; } = string.Empty;
    public List<CardFieldDefinitionV2> Columns { get; set; } = new();
}

public sealed class CardFieldDefinitionV2
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public bool Required { get; set; }
    public bool Readonly { get; set; }
    public object? DefaultValue { get; set; }
    public string? Placeholder { get; set; }
    public string? Unit { get; set; }
    public int? Precision { get; set; }
    public string? Group { get; set; }
    public FieldDisplayBehavior Display { get; set; } = new();
    public FieldDataSourceConfig? DataSource { get; set; }
    public List<FieldOption> Options { get; set; } = new();
    public bool Sensitive { get; set; }
    public string? MaskPattern { get; set; }
}

public sealed class FieldDisplayBehavior
{
    public bool ListVisible { get; set; } = true;
    public bool SummaryVisible { get; set; }
    public bool ApprovalVisible { get; set; } = true;
}

public sealed class FieldDataSourceConfig
{
    public string Type { get; set; } = "static";
    public string? Source { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new();
}

public sealed class FieldOption
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
