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
[Route("api/points/rules")]
public class PointRuleController : ControllerBase
{
    private readonly IPointRuleService _service;

    public PointRuleController(IPointRuleService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>规则列表</summary>
    [HttpGet]
    [RequirePermission(PointsPermissions.RuleView)]
    public async Task<ApiResult<PagedResult<PointRuleListDto>>> GetPagedList([FromQuery] PointRulePagedRequest request)
    {
        return await _service.GetPagedListAsync(GetOrgId(), request);
    }

    /// <summary>规则详情</summary>
    [HttpGet("{id}")]
    [RequirePermission(PointsPermissions.RuleView)]
    public async Task<ApiResult<PointRuleDetailDto>> GetById(long id)
    {
        return await _service.GetByIdAsync(id);
    }

    /// <summary>创建规则</summary>
    [HttpPost]
    [RequirePermission(PointsPermissions.RuleManage)]
    public async Task<ApiResult<PointRuleDetailDto>> Create([FromBody] CreatePointRuleRequest request)
    {
        return await _service.CreateAsync(GetOrgId(), request);
    }

    /// <summary>更新规则</summary>
    [HttpPut("{id}")]
    [RequirePermission(PointsPermissions.RuleManage)]
    public async Task<ApiResult<PointRuleDetailDto>> Update(long id, [FromBody] UpdatePointRuleRequest request)
    {
        return await _service.UpdateAsync(id, request);
    }

    /// <summary>删除规则</summary>
    [HttpDelete("{id}")]
    [RequirePermission(PointsPermissions.RuleManage)]
    public async Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(id);
    }

    /// <summary>启用/禁用规则</summary>
    [HttpPut("{id}/toggle")]
    [RequirePermission(PointsPermissions.RuleManage)]
    public async Task<ApiResult<bool>> Toggle(long id)
    {
        return await _service.ToggleAsync(id);
    }
}
