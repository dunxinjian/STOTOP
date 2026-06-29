using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Task.Controllers;

[Authorize]
[ApiController]
[Route("api/task")]
public class KeyResultController : ControllerBase
{
    private readonly IKeyResultService _service;

    public KeyResultController(IKeyResultService service)
    {
        _service = service;
    }

    /// <summary>获取目标下的KR列表</summary>
    [HttpGet("goals/{goalId}/key-results")]
    [RequirePermission(TaskPermissions.KrView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<KeyResultListDto>>> GetByGoalId(long goalId)
    {
        return await _service.GetByGoalIdAsync(goalId);
    }

    /// <summary>创建KR</summary>
    [HttpPost("goals/{goalId}/key-results")]
    [RequirePermission(TaskPermissions.KrCreate)]
    public async global::System.Threading.Tasks.Task<ApiResult<KeyResultListDto>> Create(long goalId, [FromBody] CreateKeyResultRequest request)
    {
        return await _service.CreateAsync(goalId, request);
    }

    /// <summary>更新KR</summary>
    [HttpPut("key-results/{id}")]
    [RequirePermission(TaskPermissions.KrEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<KeyResultListDto>> Update(long id, [FromBody] UpdateKeyResultRequest request)
    {
        return await _service.UpdateAsync(id, request);
    }

    /// <summary>更新KR当前值（自动重算进度）</summary>
    [HttpPut("key-results/{id}/progress")]
    [RequirePermission(TaskPermissions.KrEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<KeyResultListDto>> UpdateProgress(long id, [FromBody] UpdateKeyResultProgressRequest request)
    {
        return await _service.UpdateProgressAsync(id, request);
    }

    /// <summary>删除KR</summary>
    [HttpDelete("key-results/{id}")]
    [RequirePermission(TaskPermissions.KrDelete)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(id);
    }
}
