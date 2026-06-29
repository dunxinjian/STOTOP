using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Handlers;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/voucher-generations")]
public class CfVoucherGenerationController : ControllerBase
{
    private readonly VoucherGenerationService _service;

    public CfVoucherGenerationController(VoucherGenerationService service)
    {
        _service = service;
    }

    /// <summary>查询凭证生成记录列表</summary>
    [HttpGet]
    [RequirePermission(CardFlowPermissions.ImportProcess)]
    public async Task<ApiResult<List<VoucherGenerationRecordDto>>> GetRecords(
        [FromQuery] long? batchId = null,
        [FromQuery] long? fileTypeId = null)
    {
        var records = await _service.GetRecordsAsync(batchId, fileTypeId);
        return ApiResult<List<VoucherGenerationRecordDto>>.Success(records);
    }

    /// <summary>查询单条记录详情</summary>
    [HttpGet("{id:long}")]
    [RequirePermission(CardFlowPermissions.ImportProcess)]
    public async Task<ApiResult<VoucherGenerationRecordDto>> GetRecord(long id)
    {
        var record = await _service.GetRecordAsync(id);
        if (record == null)
            return ApiResult<VoucherGenerationRecordDto>.Fail("记录不存在");
        return ApiResult<VoucherGenerationRecordDto>.Success(record);
    }

    /// <summary>重试生成凭证</summary>
    [HttpPost("{id:long}/retry")]
    [RequirePermission(CardFlowPermissions.ImportProcess)]
    public async Task<ApiResult> Retry(long id)
    {
        var result = await _service.RetryAsync(id);
        return result.Success
            ? ApiResult.Ok(result.Message ?? "重试成功")
            : ApiResult.Fail(result.Message ?? "重试失败");
    }
}
