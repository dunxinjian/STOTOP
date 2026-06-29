using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Services.Exception;

namespace STOTOP.Module.Quality.Controllers;

[Authorize]
[ApiController]
[Route("api/quality")]
public class ExceptionController : ControllerBase
{
    private readonly IExceptionService _service;

    public ExceptionController(IExceptionService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>异常单分页列表</summary>
    [HttpGet("exceptions")]
    [RequirePermission(QualityPermissions.ExceptionView)]
    public async Task<ApiResult<PagedResult<ExceptionListDto>>> GetPaged([FromQuery] ExceptionPagedRequest request)
    {
        return await _service.GetPagedAsync(GetOrgId(), request);
    }

    /// <summary>异常单详情</summary>
    [HttpGet("exceptions/{id}")]
    [RequirePermission(QualityPermissions.ExceptionView)]
    public async Task<ApiResult<ExceptionDetailDto>> GetDetail(long id)
    {
        return await _service.GetDetailAsync(GetOrgId(), id);
    }

    /// <summary>创建异常单</summary>
    [HttpPost("exceptions")]
    [RequirePermission(QualityPermissions.ExceptionManage)]
    public async Task<ApiResult<ExceptionListDto>> Create([FromBody] CreateExceptionRequest request)
    {
        return await _service.CreateAsync(GetOrgId(), GetUserId(), request);
    }

    /// <summary>更新异常单</summary>
    [HttpPut("exceptions/{id}")]
    [RequirePermission(QualityPermissions.ExceptionManage)]
    public async Task<ApiResult<ExceptionListDto>> Update(long id, [FromBody] UpdateExceptionRequest request)
    {
        return await _service.UpdateAsync(GetOrgId(), GetUserId(), id, request);
    }

    /// <summary>删除异常单</summary>
    [HttpDelete("exceptions/{id}")]
    [RequirePermission(QualityPermissions.ExceptionManage)]
    public async Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(GetOrgId(), GetUserId(), id);
    }

    /// <summary>派发异常单</summary>
    [HttpPost("exceptions/{id}/dispatch")]
    [RequirePermission(QualityPermissions.ExceptionManage)]
    public async Task<ApiResult<bool>> Dispatch(long id, [FromBody] DispatchExceptionRequest request)
    {
        return await _service.DispatchAsync(GetOrgId(), GetUserId(), id, request);
    }

    /// <summary>关闭异常单</summary>
    [HttpPost("exceptions/{id}/close")]
    [RequirePermission(QualityPermissions.ExceptionManage)]
    public async Task<ApiResult<bool>> Close(long id, [FromBody] CloseExceptionRequest request)
    {
        return await _service.CloseAsync(GetOrgId(), GetUserId(), id, request);
    }

    /// <summary>转派异常单</summary>
    [HttpPost("exceptions/{id}/reassign")]
    [RequirePermission(QualityPermissions.ExceptionManage)]
    public async Task<ApiResult<bool>> Reassign(long id, [FromBody] ReassignExceptionRequest request)
    {
        return await _service.ReassignAsync(GetOrgId(), GetUserId(), id, request);
    }

    /// <summary>异常状态统计</summary>
    [HttpGet("exceptions/count-by-status")]
    [RequirePermission(QualityPermissions.ExceptionView)]
    public async Task<ApiResult<ExceptionCountByStatusDto>> GetCountByStatus()
    {
        return await _service.GetCountByStatusAsync(GetOrgId());
    }
}
