using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface IKeyResultService
{
    Task<ApiResult<List<KeyResultListDto>>> GetByGoalIdAsync(long goalId);
    Task<ApiResult<KeyResultListDto>> CreateAsync(long goalId, CreateKeyResultRequest request);
    Task<ApiResult<KeyResultListDto>> UpdateAsync(long id, UpdateKeyResultRequest request);
    Task<ApiResult<KeyResultListDto>> UpdateProgressAsync(long id, UpdateKeyResultProgressRequest request);
    Task<ApiResult<bool>> DeleteAsync(long id);
}
