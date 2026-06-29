using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 政策返利方案管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/policy-rebate")]
public class PolicyRebateController : ControllerBase
{
    private readonly IPolicyRebateService _service;

    public PolicyRebateController(IPolicyRebateService service)
    {
        _service = service;
    }

    /// <summary>
    /// 分页查询方案列表
    /// </summary>
    [HttpGet]
    [RequirePermission(ExpressPermissions.PolicyRebateView)]
    public async Task<ApiResult<PagedResult<PolicyRebateListItemDto>>> GetList([FromQuery] PolicyRebateQueryRequest request)
    {
        var result = await _service.GetPagedListAsync(request);
        return ApiResult<PagedResult<PolicyRebateListItemDto>>.Success(result);
    }

    /// <summary>
    /// 方案详情（含阶梯+规则）
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(ExpressPermissions.PolicyRebateView)]
    public async Task<ApiResult<PolicyRebateDetailDto>> GetDetail(long id)
    {
        var result = await _service.GetDetailAsync(id);
        if (result == null)
            return ApiResult<PolicyRebateDetailDto>.Fail("返利方案不存在");
        return ApiResult<PolicyRebateDetailDto>.Success(result);
    }

    /// <summary>
    /// 创建方案
    /// </summary>
    [HttpPost]
    [RequirePermission(ExpressPermissions.PolicyRebateCreate)]
    public async Task<ApiResult<PolicyRebateDetailDto>> Create([FromBody] CreatePolicyRebateRequest request)
    {
        var result = await _service.CreateAsync(request);
        return ApiResult<PolicyRebateDetailDto>.Success(result, "创建返利方案成功");
    }

    /// <summary>
    /// 更新方案
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(ExpressPermissions.PolicyRebateEdit)]
    public async Task<ApiResult<PolicyRebateDetailDto>> Update(long id, [FromBody] UpdatePolicyRebateRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        if (result == null)
            return ApiResult<PolicyRebateDetailDto>.Fail("返利方案不存在");
        return ApiResult<PolicyRebateDetailDto>.Success(result, "更新返利方案成功");
    }

    /// <summary>
    /// 删除方案
    /// </summary>
    [HttpDelete("{id}")]
    [RequirePermission(ExpressPermissions.PolicyRebateDelete)]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result) return ApiResult.Fail("返利方案不存在");
        return ApiResult.Ok("删除返利方案成功");
    }

    /// <summary>
    /// 启用方案
    /// </summary>
    [HttpPut("{id}/enable")]
    [RequirePermission(ExpressPermissions.PolicyRebateEdit)]
    public async Task<ApiResult> Enable(long id)
    {
        var result = await _service.EnableAsync(id);
        if (!result) return ApiResult.Fail("返利方案不存在");
        return ApiResult.Ok("启用成功");
    }

    /// <summary>
    /// 停用方案
    /// </summary>
    [HttpPut("{id}/disable")]
    [RequirePermission(ExpressPermissions.PolicyRebateEdit)]
    public async Task<ApiResult> Disable(long id)
    {
        var result = await _service.DisableAsync(id);
        if (!result) return ApiResult.Fail("返利方案不存在");
        return ApiResult.Ok("停用成功");
    }
}
