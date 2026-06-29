namespace STOTOP.Module.CardFlow.Models.Schema;

public sealed class StageViewProfile
{
    public string? ProfileName { get; set; }
    public List<StageViewSection> Sections { get; set; } = new();
    public List<StageComponentSection> ComponentSections { get; set; } = new();
    public List<StageComponentRef> Components { get; set; } = new();
    public Dictionary<string, StageFieldAccessRule> FieldAccess { get; set; } = new();
    public Dictionary<string, StageDetailAccessRule> DetailAccess { get; set; } = new();
    public Dictionary<string, StageComponentAccessRule> ComponentAccess { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public StageSummaryProfile? Summary { get; set; }
}

public sealed class StageComponentSection
{
    public string Key { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Region { get; set; } = "main";
    public List<string> ComponentIds { get; set; } = new();
}

public sealed class StageViewSection
{
    public string Key { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Type { get; set; } = "fields";
    public List<StageViewFieldRef> Fields { get; set; } = new();
}

public sealed class StageViewFieldRef
{
    public string FieldKey { get; set; } = string.Empty;
    public string? Label { get; set; }
}

public sealed class StageFieldAccessRule
{
    public string Access { get; set; } = "readonly";
    public bool? Required { get; set; }
    public string? MaskPattern { get; set; }
    public object? DefaultValue { get; set; }
}

public sealed class StageDetailAccessRule
{
    public string Access { get; set; } = "readonly";
    public bool? Required { get; set; }
    public string? MaskPattern { get; set; }
    public object? DefaultValue { get; set; }
}

public sealed class StageActionPolicy
{
    public List<string> AllowedActions { get; set; } = new();
}

public sealed class StageSummaryProfile
{
    public List<string> Fields { get; set; } = new();
}
