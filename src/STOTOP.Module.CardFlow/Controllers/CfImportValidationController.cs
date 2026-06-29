using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Validation;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/import-validation")]
public class CfImportValidationController : ControllerBase
{
    private readonly IImportCalculationValidationService _validationService;

    public CfImportValidationController(IImportCalculationValidationService validationService)
    {
        _validationService = validationService;
    }

    [HttpGet("batches/{batchId:long}/summary")]
    [RequirePermission(CardFlowPermissions.ImportValidation)]
    public async Task<ApiResult<ImportValidationSummaryDto>> GetSummary(long batchId, CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _validationService.GetSummaryAsync(batchId, GetCurrentOrgId(), cancellationToken);
            return ApiResult<ImportValidationSummaryDto>.Success(summary);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ImportValidationSummaryDto>.Fail(ex.Message, 404);
        }
    }

    [HttpPost("batches/{batchId:long}/run")]
    [RequirePermission(CardFlowPermissions.ImportValidation)]
    public async Task<ApiResult<ImportValidationReportDto>> Run(
        long batchId,
        [FromBody] ImportValidationRunRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await _validationService.RunAsync(batchId, GetCurrentOrgId(), request, cancellationToken);
            return ApiResult<ImportValidationReportDto>.Success(report);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ImportValidationReportDto>.Fail(ex.Message, 404);
        }
    }

    [HttpGet("batches/{batchId:long}/rows/{rowId:long}")]
    [RequirePermission(CardFlowPermissions.ImportValidation)]
    public async Task<ApiResult<ImportValidationFindingDto>> GetRowDetail(
        long batchId,
        long rowId,
        CancellationToken cancellationToken)
    {
        try
        {
            var finding = await _validationService.GetRowDetailAsync(
                batchId,
                rowId,
                GetCurrentOrgId(),
                cancellationToken);

            return finding == null
                ? ApiResult<ImportValidationFindingDto>.Fail("未找到该行的验证异常。", 404)
                : ApiResult<ImportValidationFindingDto>.Success(finding);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ImportValidationFindingDto>.Fail(ex.Message, 404);
        }
    }

    private long GetCurrentOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
}
