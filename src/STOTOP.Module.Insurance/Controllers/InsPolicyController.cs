using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Insurance.Dtos;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Controllers;

/// <summary>
/// 保单管理控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/insurance/policies")]
public class InsPolicyController : ControllerBase
{
    private readonly IInsPolicyService _service;

    public InsPolicyController(IInsPolicyService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取保单列表（分页）
    /// </summary>
    [HttpGet]
    [RequirePermission(InsurancePermissions.PolicyView)]
    public async Task<ApiResult<PagedResult<InsPolicyListItemDto>>> GetList([FromQuery] InsPolicyQueryRequest request)
    {
        var result = await _service.GetListAsync(request);
        return ApiResult<PagedResult<InsPolicyListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取保单详情
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(InsurancePermissions.PolicyView)]
    public async Task<ApiResult<InsPolicyDto>> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
        {
            return ApiResult<InsPolicyDto>.Fail("保单不存在");
        }
        return ApiResult<InsPolicyDto>.Success(result);
    }

    /// <summary>
    /// 创建保单
    /// </summary>
    [HttpPost]
    [RequirePermission(InsurancePermissions.PolicyCreate)]
    public async Task<ApiResult<InsPolicyDto>> Create([FromBody] CreateInsPolicyRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return ApiResult<InsPolicyDto>.Success(result, "创建保单成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<InsPolicyDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新保单
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(InsurancePermissions.PolicyEdit)]
    public async Task<ApiResult<InsPolicyDto>> Update(long id, [FromBody] UpdateInsPolicyRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request);
            if (result == null)
            {
                return ApiResult<InsPolicyDto>.Fail("保单不存在");
            }
            return ApiResult<InsPolicyDto>.Success(result, "更新保单成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<InsPolicyDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取即将到期的保单列表
    /// </summary>
    [HttpGet("expiring")]
    [RequirePermission(InsurancePermissions.PolicyView)]
    public async Task<ApiResult<List<InsPolicyListItemDto>>> GetExpiring([FromQuery] int days = 30)
    {
        var result = await _service.GetExpiringAsync(days);
        return ApiResult<List<InsPolicyListItemDto>>.Success(result);
    }

    /// <summary>
    /// 根据业务对象获取保单列表
    /// </summary>
    [HttpGet("by-object")]
    [RequirePermission(InsurancePermissions.PolicyView)]
    public async Task<ApiResult<List<InsPolicyListItemDto>>> GetByObject([FromQuery] int bizType, [FromQuery] long objectId)
    {
        var result = await _service.GetByObjectAsync(bizType, objectId);
        return ApiResult<List<InsPolicyListItemDto>>.Success(result);
    }
}
