namespace STOTOP.Module.CardFlow.AutoPlugin;

/// <summary>
/// 插件进度上报实现，适配 IAutoPluginProgressReporter 的 SignalR + DB 持久化能力
/// </summary>
public class PluginProgressReporter : IPluginProgressReporter
{
    private readonly IAutoPluginProgressReporter _reporter;

    public PluginProgressReporter(IAutoPluginProgressReporter reporter)
    {
        _reporter = reporter;
    }

    public async Task ReportProgressAsync(long batchId, int processedRows, int totalRows, string? currentStep = null)
    {
        await _reporter.ReportAsync(batchId, currentStep ?? "processing", processedRows, totalRows);
    }

    public async Task ReportErrorAsync(long batchId, int rowIndex, string errorMessage)
    {
        await _reporter.ReportPhaseAsync(batchId, "error", $"Row {rowIndex}: {errorMessage}");
    }

    public async Task ReportCompletedAsync(long batchId, bool success, string? message = null)
    {
        await _reporter.ReportPhaseAsync(batchId, success ? "completed" : "failed", message ?? "");
    }
}
