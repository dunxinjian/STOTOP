using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Insurance.Dtos;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Controllers;

/// <summary>
/// 出险记录管理控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/insurance/claims")]
public class InsClaimController : ControllerBase
{
    private readonly IInsClaimService _service;

    public InsClaimController(IInsClaimService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取出险记录列表（分页）
    /// </summary>
    [HttpGet]
    [RequirePermission(InsurancePermissions.ClaimView)]
    public async Task<ApiResult<PagedResult<InsClaimListItemDto>>> GetList([FromQuery] InsClaimQueryRequest request)
    {
        var result = await _service.GetListAsync(request);
        return ApiResult<PagedResult<InsClaimListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取出险记录详情
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(InsurancePermissions.ClaimView)]
    public async Task<ApiResult<InsClaimDto>> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
        {
            return ApiResult<InsClaimDto>.Fail("出险记录不存在");
        }
        return ApiResult<InsClaimDto>.Success(result);
    }

    /// <summary>
    /// 创建出险记录
    /// </summary>
    [HttpPost]
    [RequirePermission(InsurancePermissions.ClaimCreate)]
    public async Task<ApiResult<InsClaimDto>> Create([FromBody] CreateInsClaimRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return ApiResult<InsClaimDto>.Success(result, "创建出险记录成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<InsClaimDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新出险记录
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(InsurancePermissions.ClaimEdit)]
    public async Task<ApiResult<InsClaimDto>> Update(long id, [FromBody] UpdateInsClaimRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request);
            if (result == null)
            {
                return ApiResult<InsClaimDto>.Fail("出险记录不存在");
            }
            return ApiResult<InsClaimDto>.Success(result, "更新出险记录成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<InsClaimDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 结案
    /// </summary>
    [HttpPut("{id}/close")]
    [RequirePermission(InsurancePermissions.ClaimClose)]
    public async Task<ApiResult> Close(long id, [FromBody] CloseClaimRequest request)
    {
        try
        {
            var result = await _service.CloseAsync(id, request.ClosedRemark);
            if (!result)
            {
                return ApiResult.Fail("出险记录不存在或已结案");
            }
            return ApiResult.Ok("结案成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}

/// <summary>
/// 结案请求
/// </summary>
public class CloseClaimRequest
{
    public string? ClosedRemark { get; set; }
}
