namespace STOTOP.Module.CardFlow.Services;

/// <summary>批次级事件推送（执行引擎调用）</summary>
public interface IBatchNotifier
{
    /// <summary>管道开始：推送预创建的插件列表</summary>
    Task PipelineStartedAsync(long batchId, IEnumerable<PluginSnapshot> plugins);

    /// <summary>插件状态变更</summary>
    Task PluginStatusChangedAsync(long batchId, int pluginIndex, string pluginName, string status, string? error = null);

    /// <summary>进度更新（行级/卡片级）</summary>
    Task ProgressUpdateAsync(long batchId, int processed, int total);
}

/// <summary>插件快照（用于 PipelineStarted 事件）</summary>
public record PluginSnapshot(string Name, int Index, int Status);
