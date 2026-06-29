using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Insurance.Dtos;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Controllers;

/// <summary>
/// 保险公司管理控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/insurance/companies")]
public class InsCompanyController : ControllerBase
{
    private readonly IInsCompanyService _service;

    public InsCompanyController(IInsCompanyService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取保险公司列表（分页）
    /// </summary>
    [HttpGet]
    [RequirePermission(InsurancePermissions.CompanyView)]
    public async Task<ApiResult<PagedResult<InsCompanyListItemDto>>> GetList([FromQuery] InsCompanyQueryRequest request)
    {
        var result = await _service.GetListAsync(request);
        return ApiResult<PagedResult<InsCompanyListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取保险公司详情
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(InsurancePermissions.CompanyView)]
    public async Task<ApiResult<InsCompanyDto>> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
        {
            return ApiResult<InsCompanyDto>.Fail("保险公司不存在");
        }
        return ApiResult<InsCompanyDto>.Success(result);
    }

    /// <summary>
    /// 创建保险公司
    /// </summary>
    [HttpPost]
    [RequirePermission(InsurancePermissions.CompanyEdit)]
    public async Task<ApiResult<InsCompanyDto>> Create([FromBody] CreateInsCompanyRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return ApiResult<InsCompanyDto>.Success(result, "创建保险公司成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<InsCompanyDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新保险公司
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(InsurancePermissions.CompanyEdit)]
    public async Task<ApiResult<InsCompanyDto>> Update(long id, [FromBody] UpdateInsCompanyRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request);
            if (result == null)
            {
                return ApiResult<InsCompanyDto>.Fail("保险公司不存在");
            }
            return ApiResult<InsCompanyDto>.Success(result, "更新保险公司成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<InsCompanyDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 切换保险公司启用/停用状态
    /// </summary>
    [HttpPut("{id}/status")]
    [RequirePermission(InsurancePermissions.CompanyEdit)]
    public async Task<ApiResult> ToggleStatus(long id)
    {
        try
        {
            var result = await _service.ToggleStatusAsync(id);
            if (!result)
            {
                return ApiResult.Fail("保险公司不存在");
            }
            return ApiResult.Ok("状态更新成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
