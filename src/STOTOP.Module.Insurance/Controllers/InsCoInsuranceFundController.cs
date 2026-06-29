using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Insurance.Dtos;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Controllers;

/// <summary>
/// 共保基金管理控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/insurance/funds")]
public class InsCoInsuranceFundController : ControllerBase
{
    private readonly ICoInsuranceFundService _service;

    public InsCoInsuranceFundController(ICoInsuranceFundService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取共保基金列表（分页）
    /// </summary>
    [HttpGet]
    [RequirePermission(InsurancePermissions.FundView)]
    public async Task<ApiResult<PagedResult<InsCoInsuranceFundListItemDto>>> GetList([FromQuery] InsCoInsuranceFundQueryRequest request)
    {
        var result = await _service.GetListAsync(request);
        return ApiResult<PagedResult<InsCoInsuranceFundListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取共保基金详情
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(InsurancePermissions.FundView)]
    public async Task<ApiResult<InsCoInsuranceFundDto>> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
        {
            return ApiResult<InsCoInsuranceFundDto>.Fail("共保基金不存在");
        }
        return ApiResult<InsCoInsuranceFundDto>.Success(result);
    }

    /// <summary>
    /// 创建共保基金
    /// </summary>
    [HttpPost]
    [RequirePermission(InsurancePermissions.FundCreate)]
    public async Task<ApiResult<InsCoInsuranceFundDto>> Create([FromBody] CreateInsCoInsuranceFundRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return ApiResult<InsCoInsuranceFundDto>.Success(result, "创建共保基金成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<InsCoInsuranceFundDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新共保基金
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(InsurancePermissions.FundEdit)]
    public async Task<ApiResult<InsCoInsuranceFundDto>> Update(long id, [FromBody] UpdateInsCoInsuranceFundRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request);
            if (result == null)
            {
                return ApiResult<InsCoInsuranceFundDto>.Fail("共保基金不存在");
            }
            return ApiResult<InsCoInsuranceFundDto>.Success(result, "更新共保基金成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<InsCoInsuranceFundDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 生成缴费记录
    /// </summary>
    [HttpPost("{id}/contributions/generate")]
    [RequirePermission(InsurancePermissions.FundEdit)]
    public async Task<ApiResult<int>> GenerateContributions(long id, [FromBody] GenerateContributionsRequest request)
    {
        try
        {
            var count = await _service.GenerateContributionsAsync(id, request.PeriodStart, request.PeriodEnd);
            return ApiResult<int>.Success(count, $"已生成 {count} 条缴费记录");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<int>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 确认缴费
    /// </summary>
    [HttpPut("contributions/{id}/confirm")]
    [RequirePermission(InsurancePermissions.FundEdit)]
    public async Task<ApiResult> ConfirmContribution(long id)
    {
        try
        {
            await _service.ConfirmContributionAsync(id);
            return ApiResult.Ok("确认缴费成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取基金缴费记录列表
    /// </summary>
    [HttpGet("{fundId}/contributions")]
    [RequirePermission(InsurancePermissions.FundView)]
    public async Task<ApiResult<PagedResult<InsFundContributionListItemDto>>> GetContributions(long fundId, [FromQuery] InsFundContributionQueryRequest request)
    {
        request.FundId = fundId;
        var result = await _service.GetContributionsAsync(request);
        return ApiResult<PagedResult<InsFundContributionListItemDto>>.Success(result);
    }
}

/// <summary>
/// 生成缴费记录请求
/// </summary>
public class GenerateContributionsRequest
{
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
}
