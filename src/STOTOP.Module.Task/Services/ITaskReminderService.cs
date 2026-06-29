using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface ITaskReminderService
{
    /// <summary>
    /// 获取任务提醒列表
    /// </summary>
    Task<ApiResult<List<TaskReminderListDto>>> GetListAsync(long taskId);

    /// <summary>
    /// 创建提醒
    /// </summary>
    Task<ApiResult<TaskReminderListDto>> CreateAsync(long taskId, CreateTaskReminderRequest request);

    /// <summary>
    /// 删除提醒
    /// </summary>
    Task<ApiResult<bool>> DeleteAsync(long id);

    /// <summary>
    /// 处理到期提醒（供Job调用）
    /// </summary>
    global::System.Threading.Tasks.Task ProcessDueRemindersAsync();
}
