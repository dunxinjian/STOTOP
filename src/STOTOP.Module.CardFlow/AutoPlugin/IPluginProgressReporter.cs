using System.Threading.Tasks;

namespace STOTOP.Module.CardFlow.AutoPlugin;

/// <summary>
/// 插件行级进度上报接口
/// </summary>
public interface IPluginProgressReporter
{
    Task ReportProgressAsync(long batchId, int processedRows, int totalRows, string? currentStep = null);
    Task ReportErrorAsync(long batchId, int rowIndex, string errorMessage);
    Task ReportCompletedAsync(long batchId, bool success, string? message = null);
}
