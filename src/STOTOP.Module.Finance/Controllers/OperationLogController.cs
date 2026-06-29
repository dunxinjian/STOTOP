using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/operation-logs")]
public class OperationLogController : ControllerBase
{
    private readonly OperationLogService _operationLogService;

    public OperationLogController(OperationLogService operationLogService)
    {
        _operationLogService = operationLogService;
    }

    /// <summary>
    /// 分页查询操作日志
    /// </summary>
    [HttpGet]
    public async Task<ApiResult<PagedResult<OperationLogDto>>> GetLogs([FromQuery] OperationLogQueryRequest request)
    {
        var result = await _operationLogService.GetLogsAsync(request);
        return ApiResult<PagedResult<OperationLogDto>>.Success(result);
    }
}
