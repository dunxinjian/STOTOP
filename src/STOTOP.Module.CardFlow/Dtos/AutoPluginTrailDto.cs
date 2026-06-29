namespace STOTOP.Module.CardFlow.Dtos;

/// <summary>
/// AutoPlugin 轨迹项 DTO
/// </summary>
public class AutoPluginTrailItemDto
{
    public string PluginName { get; set; } = string.Empty;
    public string PluginType { get; set; } = string.Empty;
    public string PluginImplType { get; set; } = string.Empty;
    public int SortIndex { get; set; }
    public bool SupportsRollback { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasSnapshot { get; set; }
    public DateTime? SnapshotTime { get; set; }
    public int? CurrentStepIndex { get; set; }
    public int? TotalSteps { get; set; }
    public string? CurrentStepName { get; set; }
    public string? CurrentStepStatus { get; set; }
}

/// <summary>
/// 批次 AutoPlugin 执行轨迹 DTO
/// </summary>
public class AutoPluginTrailDto
{
    public long BatchId { get; set; }
    public string FlowName { get; set; } = string.Empty;
    public int CurrentPluginIndex { get; set; }
    public string BatchStatus { get; set; } = string.Empty;
    public List<AutoPluginTrailItemDto> AutoPlugins { get; set; } = new();
}
