using STOTOP.Module.Workflow.DTOs;

namespace STOTOP.Module.Workflow.Services.Interfaces;

public interface IWorkItemCallback
{
    // 工作项完成时的回调（通知业务系统恢复处理）
    Task OnCompletedAsync(long workItemId, string? result);

    // 工作项取消时的回调
    Task OnCancelledAsync(long workItemId, string? reason);

    // 注册回调处理器
    void RegisterHandler(string module, string bizType, Func<CallbackContext, Task> handler);
}
