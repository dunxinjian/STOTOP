namespace STOTOP.Module.CardFlow.Services.Interfaces;

/// <summary>
/// 导入服务接口（从 DC 迁移至 CardFlow）
/// </summary>
public interface IImportService
{
    /// <summary>重试指定批次的导入流程</summary>
    Task RetryBatchAsync(long batchId);
}
