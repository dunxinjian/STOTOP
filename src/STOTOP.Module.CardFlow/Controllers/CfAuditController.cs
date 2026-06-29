using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Import;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/audit")]
public class CfAuditController : ControllerBase
{
    private readonly AuditTrailService _auditService;

    public CfAuditController(AuditTrailService auditService)
    {
        _auditService = auditService;
    }

    /// <summary>凭证来源追溯</summary>
    [HttpGet("voucher-trace/{voucherId:long}")]
    [RequirePermission(CardFlowPermissions.Home)]
    public async Task<ApiResult<VoucherTraceDto?>> TraceVoucherSource(long voucherId)
    {
        var data = await _auditService.TraceVoucherSourceAsync(voucherId);
        if (data == null)
            return ApiResult<VoucherTraceDto?>.Fail("未找到该凭证的导入来源记录");
        return ApiResult<VoucherTraceDto?>.Success(data);
    }

    /// <summary>批次审计信息</summary>
    [HttpGet("batch/{batchId:long}")]
    [RequirePermission(CardFlowPermissions.Home)]
    public async Task<ApiResult<BatchAuditDto?>> GetBatchAudit(long batchId)
    {
        var data = await _auditService.GetBatchAuditAsync(batchId);
        if (data == null)
            return ApiResult<BatchAuditDto?>.Fail("批次不存在");
        return ApiResult<BatchAuditDto?>.Success(data);
    }
}
