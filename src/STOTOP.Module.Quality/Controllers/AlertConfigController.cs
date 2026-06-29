using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Services.Alert;

namespace STOTOP.Module.Quality.Controllers;

[Authorize]
[ApiController]
[Route("api/quality/alert-configs")]
public class AlertConfigController : ControllerBase
{
    private readonly IAlertConfigService _service;

    public AlertConfigController(IAlertConfigService service)
    {
        _service = service;
    }

    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>预警配置分页列表</summary>
    [HttpGet]
    [RequirePermission(QualityPermissions.AlertView)]
    public async Task<ApiResult<PagedResult<AlertConfigDto>>> GetPaged([FromQuery] AlertConfigPagedRequest request)
    {
        return await _service.GetPagedAsync(GetOrgId(), request);
    }

    /// <summary>预警配置详情</summary>
    [HttpGet("{id}")]
    [RequirePermission(QualityPermissions.AlertView)]
    public async Task<ApiResult<AlertConfigDto>> GetById(long id)
    {
        return await _service.GetByIdAsync(GetOrgId(), id);
    }

    /// <summary>创建预警配置</summary>
    [HttpPost]
    [RequirePermission(QualityPermissions.AlertManage)]
    public async Task<ApiResult<AlertConfigDto>> Create([FromBody] CreateAlertConfigRequest request)
    {
        return await _service.CreateAsync(GetOrgId(), request);
    }

    /// <summary>更新预警配置</summary>
    [HttpPut("{id}")]
    [RequirePermission(QualityPermissions.AlertManage)]
    public async Task<ApiResult<AlertConfigDto>> Update(long id, [FromBody] UpdateAlertConfigRequest request)
    {
        return await _service.UpdateAsync(GetOrgId(), id, request);
    }

    /// <summary>删除预警配置</summary>
    [HttpDelete("{id}")]
    [RequirePermission(QualityPermissions.AlertManage)]
    public async Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(GetOrgId(), id);
    }

    /// <summary>启用/禁用预警配置</summary>
    [HttpPost("{id}/toggle")]
    [RequirePermission(QualityPermissions.AlertManage)]
    public async Task<ApiResult<bool>> Toggle(long id)
    {
        return await _service.ToggleAsync(GetOrgId(), id);
    }
}
