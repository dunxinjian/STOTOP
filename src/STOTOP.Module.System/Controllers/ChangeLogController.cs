using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

[Route("api/system/change-logs")]
[ApiController]
[Authorize]
public class ChangeLogController : ControllerBase
{
    private readonly IChangeLogService _changeLogService;

    public ChangeLogController(IChangeLogService changeLogService)
    {
        _changeLogService = changeLogService;
    }

    [HttpGet]
    public async Task<ApiResult<object>> GetList([FromQuery] ChangeLogQueryRequest request)
    {
        var (items, total) = await _changeLogService.GetPagedListAsync(request);
        return ApiResult<object>.Success(new { items, total });
    }

    [HttpGet("by-business")]
    public async Task<ApiResult<List<ChangeLogDto>>> GetByBusiness([FromQuery] string businessType, [FromQuery] long businessId)
    {
        var result = await _changeLogService.GetByBusinessAsync(businessType, businessId);
        return ApiResult<List<ChangeLogDto>>.Success(result);
    }
}
