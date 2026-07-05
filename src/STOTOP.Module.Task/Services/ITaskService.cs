using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface ITaskService
{
    Task<ApiResult<PagedResult<TaskListDto>>> GetPagedListAsync(TaskPagedRequest query, long orgId, long currentUserId, bool isAdmin);
    Task<ApiResult<TaskDetailDto>> GetByIdAsync(long id);
    Task<ApiResult<TaskDetailDto>> CreateAsync(CreateTaskRequest request, long orgId, long creatorId);
    Task<ApiResult<TaskDetailDto>> UpdateAsync(long id, UpdateTaskRequest request);
    Task<ApiResult<TaskDetailDto>> ChangeStatusAsync(long id, ChangeTaskStatusRequest request);
    Task<ApiResult<TaskDetailDto>> SetPriorityAsync(long id, int priority);
    Task<ApiResult<TaskDetailDto>> AssignAsync(long id, AssignTaskRequest request);
    Task<ApiResult<TaskDetailDto>> CreateSubtaskAsync(long parentId, CreateTaskRequest request, long orgId, long creatorId);
    Task<ApiResult<List<TaskListDto>>> GetSubtasksAsync(long parentId);
    Task<ApiResult<bool>> SetVisibilityAsync(long id, SetTaskVisibilityRequest request);
    Task<ApiResult<List<TaskDependencyDto>>> GetDependenciesAsync(long id);
    Task<ApiResult<TaskDependencyDto>> AddDependencyAsync(long id, AddTaskDependencyRequest request);
    Task<ApiResult<bool>> RemoveDependencyAsync(long id, long depId);
    Task<ApiResult<List<TagSimpleDto>>> GetTagsAsync(long id);
    Task<ApiResult<bool>> SetTagsAsync(long id, SetTaskTagsRequest request);
    Task<ApiResult<PagedResult<TaskListDto>>> GetMyTasksAsync(long orgId, long currentUserId);
    /// <summary>
    /// WorkHub 专用：我的未完成任务精简列表（可见性谓词同 GetMyTasksAsync + F状态&lt;3 下推 + Take(take)）。
    /// 不跑 EnrichTaskListDtos（跳过子任务统计/标签等附加查询），仅补 ProjectName/AssigneeName 保证 Summary 不空。
    /// </summary>
    Task<List<TaskListDto>> GetMyPendingTasksLiteAsync(long orgId, long currentUserId, int take = 20);
    Task<int> GetMyPendingCountAsync(long orgId, long currentUserId);
    Task<ApiResult<bool>> DeleteAsync(long id);
    Task<ApiResult<bool>> BatchUpdateAsync(List<long> taskIds, int? status, long? assigneeId);
}
