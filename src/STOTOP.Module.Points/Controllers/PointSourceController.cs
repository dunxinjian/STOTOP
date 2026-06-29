using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Points.Controllers;

[Authorize]
[ApiController]
[Route("api/points/sources")]
public class PointSourceController : ControllerBase
{
    private readonly IPointSourceService _service;

    public PointSourceController(IPointSourceService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>获取来源列表</summary>
    [HttpGet]
    [RequirePermission(PointsPermissions.SourceManage)]
    public async Task<ApiResult<List<PointSourceDto>>> GetList()
    {
        return await _service.GetListAsync(GetOrgId());
    }

    /// <summary>创建来源</summary>
    [HttpPost]
    [RequirePermission(PointsPermissions.SourceManage)]
    public async Task<ApiResult<PointSourceDto>> Create([FromBody] CreatePointSourceRequest request)
    {
        return await _service.CreateAsync(GetOrgId(), request);
    }

    /// <summary>更新来源</summary>
    [HttpPut("{id}")]
    [RequirePermission(PointsPermissions.SourceManage)]
    public async Task<ApiResult<PointSourceDto>> Update(long id, [FromBody] UpdatePointSourceRequest request)
    {
        return await _service.UpdateAsync(id, request);
    }

    /// <summary>启用/禁用来源</summary>
    [HttpPut("{id}/toggle")]
    [RequirePermission(PointsPermissions.SourceManage)]
    public async Task<ApiResult<bool>> Toggle(long id)
    {
        return await _service.ToggleAsync(id);
    }
}
