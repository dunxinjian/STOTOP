using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/invoices")]
public class InvoiceController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoiceController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    [HttpPost("import")]
    [RequirePermission(FinancePermissions.InvoiceManage)]
    public async Task<ApiResult<object>> Import(IFormFile file, [FromQuery] long accountSetId = 0)
    {
        if (file == null || file.Length == 0)
            return ApiResult<object>.Fail("请选择要导入的文件");

        try
        {
            using var stream = file.OpenReadStream();
            var count = await _invoiceService.ImportInvoicesAsync(stream, accountSetId);
            return ApiResult<object>.Success(new { count }, $"成功导入 {count} 张发票");
        }
        catch (Exception ex)
        {
            return ApiResult<object>.Fail($"导入失败: {ex.Message}");
        }
    }

    [HttpGet]
    public async Task<ApiResult<InvoicePagedResult>> GetList([FromQuery] InvoiceQueryRequest request, [FromQuery] long accountSetId = 0)
    {
        var result = await _invoiceService.GetInvoicesAsync(request, accountSetId);
        return ApiResult<InvoicePagedResult>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<InvoiceDto>> GetById(long id)
    {
        var result = await _invoiceService.GetInvoiceByIdAsync(id);
        if (result == null)
            return ApiResult<InvoiceDto>.Fail("发票不存在");
        return ApiResult<InvoiceDto>.Success(result);
    }

    [HttpPost("{id}/match")]
    [RequirePermission(FinancePermissions.InvoiceManage)]
    public async Task<ApiResult> Match(long id, [FromBody] InvoiceMatchRequest request, [FromQuery] long accountSetId = 0)
    {
        var result = await _invoiceService.MatchInvoiceAsync(id, request.VoucherId, accountSetId);
        if (!result)
            return ApiResult.Fail("匹配失败，发票或凭证不存在");
        return ApiResult.Ok("匹配成功");
    }

    [HttpPost("{id}/generate-voucher")]
    [RequirePermission(FinancePermissions.InvoiceManage)]
    public async Task<ApiResult<VoucherDto>> GenerateVoucher(long id, [FromBody] InvoiceGenerateVoucherRequest request, [FromQuery] long accountSetId = 0)
    {
        try
        {
            var result = await _invoiceService.GenerateVoucherFromInvoiceAsync(id, accountSetId, request);
            return ApiResult<VoucherDto>.Success(result, "凭证生成成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VoucherDto>.Fail(ex.Message);
        }
    }

    [HttpGet("tax-summary")]
    public async Task<ApiResult<List<TaxSummaryDto>>> GetTaxSummary([FromQuery] int year, [FromQuery] long accountSetId = 0)
    {
        var result = await _invoiceService.GetTaxSummaryAsync(year, accountSetId);
        return ApiResult<List<TaxSummaryDto>>.Success(result);
    }
}
