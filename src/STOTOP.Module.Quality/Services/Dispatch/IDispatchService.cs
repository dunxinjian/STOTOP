using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Services.Dispatch;

public interface IDispatchService
{
    /// <summary>派发异常单到OA流程</summary>
    Task<ApiResult<long>> DispatchToOAAsync(long orgId, long exceptionId, long assigneeId);
    /// <summary>派发异常单到工作任务</summary>
    Task<ApiResult<long>> DispatchToTaskAsync(long orgId, long exceptionId, long assigneeId);
    /// <summary>派发异常单为消息预警</summary>
    Task<ApiResult<bool>> DispatchAsAlertAsync(long orgId, long exceptionId, long assigneeId);
}
