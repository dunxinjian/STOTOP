using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Services.Rule;

namespace STOTOP.Module.Quality.Controllers;

[Authorize]
[ApiController]
[Route("api/quality")]
public class QualityRuleController : ControllerBase
{
    private readonly IQualityRuleService _service;

    public QualityRuleController(IQualityRuleService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>规则分页列表</summary>
    [HttpGet("rules")]
    [RequirePermission(QualityPermissions.RuleView)]
    public async Task<ApiResult<PagedResult<QualityRuleDto>>> GetPaged([FromQuery] RulePagedRequest request)
    {
        return await _service.GetPagedAsync(GetOrgId(), request);
    }

    /// <summary>规则详情</summary>
    [HttpGet("rules/{id}")]
    [RequirePermission(QualityPermissions.RuleView)]
    public async Task<ApiResult<QualityRuleDetailDto>> GetById(long id)
    {
        return await _service.GetByIdAsync(GetOrgId(), id);
    }

    /// <summary>创建规则</summary>
    [HttpPost("rules")]
    [RequirePermission(QualityPermissions.RuleManage)]
    public async Task<ApiResult<QualityRuleDto>> Create([FromBody] CreateRuleRequest request)
    {
        return await _service.CreateAsync(GetOrgId(), GetUserId(), request);
    }

    /// <summary>更新规则</summary>
    [HttpPut("rules/{id}")]
    [RequirePermission(QualityPermissions.RuleManage)]
    public async Task<ApiResult<QualityRuleDto>> Update(long id, [FromBody] UpdateRuleRequest request)
    {
        return await _service.UpdateAsync(GetOrgId(), GetUserId(), id, request);
    }

    /// <summary>删除规则</summary>
    [HttpDelete("rules/{id}")]
    [RequirePermission(QualityPermissions.RuleManage)]
    public async Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(GetOrgId(), id);
    }

    /// <summary>启用/禁用规则</summary>
    [HttpPost("rules/{id}/toggle")]
    [RequirePermission(QualityPermissions.RuleManage)]
    public async Task<ApiResult<bool>> Toggle(long id)
    {
        return await _service.ToggleAsync(GetOrgId(), id);
    }
}
