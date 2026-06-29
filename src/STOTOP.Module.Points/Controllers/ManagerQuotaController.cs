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
[Route("api/points/quotas")]
public class ManagerQuotaController : ControllerBase
{
    private readonly IManagerQuotaService _service;

    public ManagerQuotaController(IManagerQuotaService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>配额列表</summary>
    [HttpGet]
    [RequirePermission(PointsPermissions.QuotaView)]
    public async Task<ApiResult<PagedResult<ManagerQuotaListDto>>> GetPagedList([FromQuery] ManagerQuotaPagedRequest request)
    {
        return await _service.GetPagedListAsync(GetOrgId(), request);
    }

    /// <summary>创建/更新配额</summary>
    [HttpPost]
    [RequirePermission(PointsPermissions.QuotaManage)]
    public async Task<ApiResult<ManagerQuotaListDto>> Save([FromBody] SaveManagerQuotaRequest request)
    {
        return await _service.SaveAsync(GetOrgId(), request);
    }

    /// <summary>我的当月配额</summary>
    [HttpGet("my")]
    [RequirePermission(PointsPermissions.QuotaView)]
    public async Task<ApiResult<MyQuotaDto>> GetMyQuota()
    {
        return await _service.GetMyQuotaAsync(GetOrgId(), GetUserId());
    }
}
