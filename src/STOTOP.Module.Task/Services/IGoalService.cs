using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface IGoalService
{
    Task<ApiResult<List<GoalTreeDto>>> GetTreeAsync(GoalTreeQueryRequest query, long orgId);
    Task<ApiResult<GoalDetailDto>> GetByIdAsync(long id);
    Task<ApiResult<GoalDetailDto>> CreateAsync(CreateGoalRequest request, long orgId, long creatorId);
    Task<ApiResult<GoalDetailDto>> UpdateAsync(long id, UpdateGoalRequest request);
    Task<ApiResult<GoalDetailDto>> DecomposeAsync(long id, DecomposeGoalRequest request, long orgId, long creatorId);
    Task<ApiResult<List<GoalListDto>>> GetChildrenAsync(long id);
    Task<ApiResult<List<TaskListDto>>> GetTasksAsync(long id);
    Task<ApiResult<bool>> RecalculateProgressAsync(long id);
}
