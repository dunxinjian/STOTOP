using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;

namespace STOTOP.Module.Quality.Services.Dispatch;

public class DispatchService : IDispatchService
{
    private readonly STOTOPDbContext _db;

    public DispatchService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<long>> DispatchToOAAsync(long orgId, long exceptionId, long assigneeId)
    {
        // TODO: 调用OA模块创建审批流程
        await global::System.Threading.Tasks.Task.CompletedTask;
        return ApiResult<long>.Success(0, "OA流程派发待实现");
    }

    public async Task<ApiResult<long>> DispatchToTaskAsync(long orgId, long exceptionId, long assigneeId)
    {
        // TODO: 调用任务模块创建工作任务
        await global::System.Threading.Tasks.Task.CompletedTask;
        return ApiResult<long>.Success(0, "工作任务派发待实现");
    }

    public async Task<ApiResult<bool>> DispatchAsAlertAsync(long orgId, long exceptionId, long assigneeId)
    {
        // TODO: 发送消息预警通知
        await global::System.Threading.Tasks.Task.CompletedTask;
        return ApiResult<bool>.Success(true, "消息预警待实现");
    }
}
