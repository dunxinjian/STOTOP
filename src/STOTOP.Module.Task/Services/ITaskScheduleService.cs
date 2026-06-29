using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface ITaskScheduleService
{
    /// <summary>
    /// 调度分页列表
    /// </summary>
    Task<ApiResult<PagedResult<TaskScheduleListDto>>> GetPagedListAsync(SchedulePagedRequest request);

    /// <summary>
    /// 创建调度（定时/周期，关联模板任务）
    /// </summary>
    Task<ApiResult<TaskScheduleListDto>> CreateAsync(CreateTaskScheduleRequest request);

    /// <summary>
    /// 更新调度
    /// </summary>
    Task<ApiResult<TaskScheduleListDto>> UpdateAsync(long id, UpdateTaskScheduleRequest request);

    /// <summary>
    /// 启用/禁用调度
    /// </summary>
    Task<ApiResult<bool>> ToggleAsync(long id);

    /// <summary>
    /// 执行调度：基于模板任务创建新任务实例，更新上次/下次执行时间
    /// </summary>
    global::System.Threading.Tasks.Task ExecuteScheduleAsync(long scheduleId);
}
