using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/definitions")]
public class RouteHitEstimateController : ControllerBase
{
    private readonly IRouteHitEstimateService _service;

    public RouteHitEstimateController(IRouteHitEstimateService service)
    {
        _service = service;
    }

    /// <summary>路由条件命中率试算：近 30 天卡片采样干跑条件（设计 B4/E6）</summary>
    [HttpPost("{id}/route-hit-estimate")]
    [RequirePermission("cardflow:definition:view")]
    public async Task<ApiResult<RouteHitEstimateDto>> Estimate(long id, [FromBody] RouteHitEstimateRequest request)
    {
        var result = await _service.EstimateAsync(id, request, HttpContext.RequestAborted);
        return ApiResult<RouteHitEstimateDto>.Success(result);
    }
}
