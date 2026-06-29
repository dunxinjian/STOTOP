using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/banking-voucher-rules")]
public class BankingVoucherRuleController : ControllerBase
{
    private readonly IVoucherAutoService _voucherAutoService;

    public BankingVoucherRuleController(IVoucherAutoService voucherAutoService)
    {
        _voucherAutoService = voucherAutoService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<VoucherRuleDto>>> GetList([FromQuery] VoucherRuleQueryRequest request)
    {
        var result = await _voucherAutoService.GetRulesAsync(request);
        return ApiResult<PagedResult<VoucherRuleDto>>.Success(result);
    }

    [HttpGet("by-priority")]
    public async Task<ApiResult<List<VoucherRuleDto>>> GetByPriority()
    {
        var result = await _voucherAutoService.GetRulesByPriorityAsync();
        return ApiResult<List<VoucherRuleDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<VoucherRuleDto>> GetById(long id)
    {
        var result = await _voucherAutoService.GetRuleByIdAsync(id);
        if (result == null)
        {
            return ApiResult<VoucherRuleDto>.Fail("凭证规则不存在");
        }
        return ApiResult<VoucherRuleDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<VoucherRuleDto>> Create([FromBody] CreateVoucherRuleRequest request)
    {
        var operatorName = User.Identity?.Name;
        var result = await _voucherAutoService.CreateRuleAsync(request, operatorName);
        return ApiResult<VoucherRuleDto>.Success(result, "创建凭证规则成功");
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<VoucherRuleDto>> Update(long id, [FromBody] UpdateVoucherRuleRequest request)
    {
        var operatorName = User.Identity?.Name;
        var result = await _voucherAutoService.UpdateRuleAsync(id, request, operatorName);
        if (result == null)
        {
            return ApiResult<VoucherRuleDto>.Fail("凭证规则不存在");
        }
        return ApiResult<VoucherRuleDto>.Success(result, "更新凭证规则成功");
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _voucherAutoService.DeleteRuleAsync(id);
        if (!result)
        {
            return ApiResult.Fail("凭证规则不存在");
        }
        return ApiResult.Ok("删除凭证规则成功");
    }

    [HttpPost("generate-voucher-draft")]
    public async Task<ApiResult<VoucherGenerateResult>> GenerateVoucherDraft()
    {
        var operatorName = User.Identity?.Name;
        var result = await _voucherAutoService.GenerateVoucherDraftAsync(operatorName);
        return ApiResult<VoucherGenerateResult>.Success(result, "凭证草稿生成完成");
    }

    [HttpGet("statistics")]
    public async Task<ApiResult<FundStatisticsDto>> GetStatistics()
    {
        var result = await _voucherAutoService.GetStatisticsAsync();
        return ApiResult<FundStatisticsDto>.Success(result);
    }
}
