using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Insurance.Dtos;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Controllers;

/// <summary>
/// 理赔审批配置控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/insurance/approval-config")]
public class InsApprovalConfigController : ControllerBase
{
    private readonly IApprovalConfigService _service;

    public InsApprovalConfigController(IApprovalConfigService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取审批配置列表
    /// </summary>
    [HttpGet]
    [RequirePermission(InsurancePermissions.ApprovalConfigView)]
    public async Task<ApiResult<PagedResult<InsApprovalConfigDto>>> GetList([FromQuery] long orgId, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetListAsync(orgId, pageIndex, pageSize);
        return ApiResult<PagedResult<InsApprovalConfigDto>>.Success(result);
    }

    /// <summary>
    /// 创建审批环节
    /// </summary>
    [HttpPost]
    [RequirePermission(InsurancePermissions.ApprovalConfigEdit)]
    public async Task<ApiResult<InsApprovalConfigDto>> Create([FromQuery] long orgId, [FromBody] CreateInsApprovalConfigRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(orgId, request);
            return ApiResult<InsApprovalConfigDto>.Success(result, "创建审批环节成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<InsApprovalConfigDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新审批环节
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(InsurancePermissions.ApprovalConfigEdit)]
    public async Task<ApiResult<InsApprovalConfigDto>> Update(long id, [FromBody] UpdateInsApprovalConfigRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request);
            if (result == null)
            {
                return ApiResult<InsApprovalConfigDto>.Fail("审批环节不存在");
            }
            return ApiResult<InsApprovalConfigDto>.Success(result, "更新审批环节成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<InsApprovalConfigDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 切换审批环节启用/停用状态
    /// </summary>
    [HttpPut("{id}/status")]
    [RequirePermission(InsurancePermissions.ApprovalConfigEdit)]
    public async Task<ApiResult> ToggleStatus(long id)
    {
        try
        {
            await _service.ToggleStatusAsync(id);
            return ApiResult.Ok("状态更新成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 调整审批环节排序
    /// </summary>
    [HttpPut("reorder")]
    [RequirePermission(InsurancePermissions.ApprovalConfigEdit)]
    public async Task<ApiResult> Reorder([FromBody] List<ApprovalStepOrderItem> items)
    {
        try
        {
            await _service.ReorderAsync(items);
            return ApiResult.Ok("排序更新成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
