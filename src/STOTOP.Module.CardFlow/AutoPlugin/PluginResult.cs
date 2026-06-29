using System.Collections.Generic;

namespace STOTOP.Module.CardFlow.AutoPlugin;

/// <summary>
/// 插件执行结果
/// </summary>
public class PluginResult
{
    public bool Success { get; set; }
    public bool IsCritical { get; set; }
    public string? Message { get; set; }
    public int ProcessedRows { get; set; }
    public int FailedRows { get; set; }
    public int SkippedRows { get; set; }
    public List<PluginRowError>? RowErrors { get; set; }
    public List<ChildBatchInfo>? CreatedBatches { get; set; }

    public static PluginResult Ok(string? message = null, int processedRows = 0) => new()
    {
        Success = true, Message = message, ProcessedRows = processedRows
    };

    public static PluginResult Fail(string message, bool isCritical = false) => new()
    {
        Success = false, Message = message, IsCritical = isCritical
    };
}

public class PluginRowError
{
    public int RowIndex { get; set; }
    public string? FieldName { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ChildBatchInfo
{
    public long BatchId { get; set; }
    public string? BatchName { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public string TargetTable { get; set; } = string.Empty;
    public int RowCount { get; set; }
}
