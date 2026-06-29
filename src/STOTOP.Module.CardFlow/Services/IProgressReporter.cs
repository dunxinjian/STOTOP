namespace STOTOP.Module.CardFlow.Services;

/// <summary>行级进度上报（插件内部调用，含节流）</summary>
public interface IProgressReporter
{
    /// <summary>上报当前进度，内部自动节流</summary>
    Task ReportAsync(int processed, int total);
}
