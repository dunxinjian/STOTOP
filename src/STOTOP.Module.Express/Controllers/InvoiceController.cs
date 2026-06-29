using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 账单管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/invoice")]
public class InvoiceController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IInvoiceReviewService _reviewService;
    private readonly InvoiceGeneratorJob _generatorJob;

    public InvoiceController(
        IInvoiceService invoiceService,
        IInvoiceReviewService reviewService,
        InvoiceGeneratorJob generatorJob)
    {
        _invoiceService = invoiceService;
        _reviewService = reviewService;
        _generatorJob = generatorJob;
    }

    /// <summary>
    /// 分页查询账单
    /// </summary>
    [HttpGet]
    [RequirePermission(ExpressPermissions.InvoiceView)]
    public async Task<ApiResult<PagedResult<InvoiceDto>>> GetList([FromQuery] InvoiceQueryRequest request)
    {
        var result = await _invoiceService.GetPagedListAsync(request);
        return ApiResult<PagedResult<InvoiceDto>>.Success(result);
    }

    /// <summary>
    /// 账单详情
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(ExpressPermissions.InvoiceView)]
    public async Task<ApiResult<InvoiceDetailDto>> GetDetail(long id)
    {
        var result = await _invoiceService.GetDetailAsync(id);
        if (result == null)
            return ApiResult<InvoiceDetailDto>.Fail("账单不存在");
        return ApiResult<InvoiceDetailDto>.Success(result);
    }

    /// <summary>
    /// 手动生成账单
    /// </summary>
    [HttpPost("generate")]
    [RequirePermission(ExpressPermissions.InvoiceGenerate)]
    public async Task<ApiResult<InvoiceDto>> Generate([FromBody] GenerateInvoiceRequest request)
    {
        try
        {
            var result = await _invoiceService.GenerateInvoiceAsync(
                request.ClientId, request.BrandCode, request.PeriodStart, request.PeriodEnd);
            return ApiResult<InvoiceDto>.Success(result, "账单生成成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<InvoiceDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 确认账单
    /// </summary>
    [HttpPut("{id}/confirm")]
    [RequirePermission(ExpressPermissions.InvoiceEdit)]
    public async Task<ApiResult<InvoiceDto>> Confirm(long id)
    {
        var result = await _invoiceService.ConfirmAsync(id);
        if (result == null)
            return ApiResult<InvoiceDto>.Fail("账单不存在");
        return ApiResult<InvoiceDto>.Success(result, "账单已确认");
    }

    /// <summary>
    /// 发送账单
    /// </summary>
    [HttpPut("{id}/send")]
    [RequirePermission(ExpressPermissions.InvoiceEdit)]
    public async Task<ApiResult<InvoiceDto>> Send(long id)
    {
        var result = await _invoiceService.SendAsync(id);
        if (result == null)
            return ApiResult<InvoiceDto>.Fail("账单不存在");
        return ApiResult<InvoiceDto>.Success(result, "账单已标记为已发送");
    }

    /// <summary>
    /// 收款
    /// </summary>
    [HttpPut("{id}/payment")]
    [RequirePermission(ExpressPermissions.InvoiceEdit)]
    public async Task<ApiResult<InvoiceDto>> ReceivePayment(long id, [FromBody] ReceivePaymentRequest request)
    {
        var result = await _invoiceService.ReceivePaymentAsync(id, request.Amount);
        if (result == null)
            return ApiResult<InvoiceDto>.Fail("账单不存在");
        return ApiResult<InvoiceDto>.Success(result, "收款成功");
    }

    /// <summary>
    /// 人工审核
    /// </summary>
    [HttpPost("{id}/review")]
    [RequirePermission(ExpressPermissions.InvoiceReview)]
    public async Task<ApiResult> Review(long id, [FromBody] ReviewInvoiceRequest request)
    {
        try
        {
            await _reviewService.ManualReviewAsync(id, request.Approved, request.Remark);
            return ApiResult.Ok(request.Approved ? "审核通过" : "审核驳回");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 反审核
    /// </summary>
    [HttpPost("{id}/reverse-review")]
    [RequirePermission(ExpressPermissions.InvoiceReview)]
    public async Task<ApiResult> ReverseReview(long id, [FromBody] ReverseReviewRequest request)
    {
        try
        {
            await _reviewService.ReverseReviewAsync(id, request.Remark);
            return ApiResult.Ok("反审核成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 触发自动出账任务
    /// </summary>
    [HttpPost("auto-generate")]
    [RequirePermission(ExpressPermissions.InvoiceGenerate)]
    public async Task<ApiResult> AutoGenerate()
    {
        try
        {
            await _generatorJob.ExecuteAsync();
            return ApiResult.Ok("自动出账任务已执行");
        }
        catch (Exception ex)
        {
            return ApiResult.Fail($"自动出账失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 审核规则列表
    /// </summary>
    [HttpGet("review-rules")]
    [RequirePermission(ExpressPermissions.InvoiceView)]
    public async Task<ApiResult<List<ReviewRuleDto>>> GetReviewRules()
    {
        var result = await _reviewService.GetRulesAsync();
        return ApiResult<List<ReviewRuleDto>>.Success(result);
    }

    /// <summary>
    /// 创建审核规则
    /// </summary>
    [HttpPost("review-rules")]
    [RequirePermission(ExpressPermissions.InvoiceEdit)]
    public async Task<ApiResult<ReviewRuleDto>> CreateReviewRule([FromBody] CreateReviewRuleRequest request)
    {
        var result = await _reviewService.CreateRuleAsync(request);
        return ApiResult<ReviewRuleDto>.Success(result, "创建审核规则成功");
    }

    /// <summary>
    /// 更新审核规则
    /// </summary>
    [HttpPut("review-rules/{id}")]
    [RequirePermission(ExpressPermissions.InvoiceEdit)]
    public async Task<ApiResult<ReviewRuleDto>> UpdateReviewRule(long id, [FromBody] UpdateReviewRuleRequest request)
    {
        var result = await _reviewService.UpdateRuleAsync(id, request);
        if (result == null)
            return ApiResult<ReviewRuleDto>.Fail("审核规则不存在");
        return ApiResult<ReviewRuleDto>.Success(result, "更新审核规则成功");
    }

    /// <summary>
    /// 删除审核规则
    /// </summary>
    [HttpDelete("review-rules/{id}")]
    [RequirePermission(ExpressPermissions.InvoiceDelete)]
    public async Task<ApiResult> DeleteReviewRule(long id)
    {
        var result = await _reviewService.DeleteRuleAsync(id);
        if (!result)
            return ApiResult.Fail("审核规则不存在");
        return ApiResult.Ok("删除审核规则成功");
    }

    #region 对账

    /// <summary>
    /// 对账详情
    /// </summary>
    [HttpGet("{id}/reconciliation")]
    [RequirePermission(ExpressPermissions.ReconciliationView)]
    public async Task<ApiResult<ReconciliationDetailDto>> GetReconciliationDetail(long id)
    {
        try
        {
            var result = await _invoiceService.GetReconciliationDetailAsync(id);
            return ApiResult<ReconciliationDetailDto>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ReconciliationDetailDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 确认对账
    /// </summary>
    [HttpPost("{id}/reconciliation/confirm")]
    [RequirePermission(ExpressPermissions.ReconciliationConfirm)]
    public async Task<ApiResult<bool>> ConfirmReconciliation(long id, [FromBody] ReconciliationConfirmRequest request)
    {
        try
        {
            var result = await _invoiceService.ConfirmReconciliationAsync(id, request);
            if (!result)
                return ApiResult<bool>.Fail("账单不存在");
            return ApiResult<bool>.Success(true, "对账确认成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 提起异议
    /// </summary>
    [HttpPost("{id}/reconciliation/dispute")]
    [RequirePermission(ExpressPermissions.ReconciliationDispute)]
    public async Task<ApiResult<bool>> RaiseDispute(long id, [FromBody] ReconciliationDisputeRequest request)
    {
        try
        {
            var result = await _invoiceService.RaiseDisputeAsync(id, request);
            if (!result)
                return ApiResult<bool>.Fail("账单不存在");
            return ApiResult<bool>.Success(true, "异议已提交");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 处理异议
    /// </summary>
    [HttpPost("{id}/reconciliation/dispute/resolve")]
    [RequirePermission(ExpressPermissions.ReconciliationResolve)]
    public async Task<ApiResult<bool>> ResolveDispute(long id, [FromBody] ReconciliationResolveRequest request)
    {
        try
        {
            var result = await _invoiceService.ResolveDisputeAsync(id, request);
            if (!result)
                return ApiResult<bool>.Fail("账单不存在");
            return ApiResult<bool>.Success(true, "异议已处理");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 导出对账明细
    /// </summary>
    [HttpGet("{id}/reconciliation/export")]
    [RequirePermission(ExpressPermissions.ReconciliationExport)]
    public async Task<IActionResult> ExportReconciliation(long id)
    {
        try
        {
            var bytes = await _invoiceService.ExportReconciliationAsync(id);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"reconciliation_{id}.xlsx");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    #endregion
}
