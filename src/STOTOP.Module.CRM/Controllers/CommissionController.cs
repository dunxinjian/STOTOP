using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CRM.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/commissions")]
public class CommissionController : ControllerBase
{
    private readonly IReferralCommissionService _service;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CommissionController(IReferralCommissionService service, IHttpContextAccessor httpContextAccessor)
    {
        _service = service;
        _httpContextAccessor = httpContextAccessor;
    }

    private long GetCurrentUserId()
    {
        var userIdObj = _httpContextAccessor.HttpContext?.Items["CurrentUserId"];
        if (userIdObj is long userId) return userId;
        return 0;
    }

    [HttpGet]
    [RequirePermission(CrmPermissions.ReferralView)]
    public async Task<ApiResult<PagedResult<CommissionDto>>> GetList([FromQuery] CommissionQueryRequest request)
    {
        var result = await _service.GetCommissionsAsync(request);
        return ApiResult<PagedResult<CommissionDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(CrmPermissions.ReferralView)]
    public async Task<ApiResult<CommissionDto>> GetById(long id)
    {
        var result = await _service.GetCommissionByIdAsync(id);
        if (result == null)
            return ApiResult<CommissionDto>.Fail("返佣记录不存在");
        return ApiResult<CommissionDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(CrmPermissions.CommissionApply)]
    public async Task<ApiResult<CommissionDto>> Create([FromBody] CreateCommissionRequest request)
    {
        var result = await _service.CreateCommissionAsync(request);
        return ApiResult<CommissionDto>.Success(result, "创建返佣申请成功");
    }

    [HttpPut("{id}/status")]
    [RequirePermission(CrmPermissions.CommissionApply)]
    public async Task<ApiResult> UpdateStatus(long id, [FromBody] UpdateStatusRequest request)
    {
        var result = await _service.UpdateCommissionStatusAsync(id, request.Status);
        if (!result)
            return ApiResult.Fail("返佣记录不存在");
        return ApiResult.Ok("更新状态成功");
    }

    /// <summary>
    /// 计算返佣金额（根据客户收入数据和返佣比例）
    /// </summary>
    [HttpPost("calc")]
    [RequirePermission(CrmPermissions.CommissionApply)]
    public async Task<ApiResult<CalcCommissionResultDto>> Calc([FromBody] CalcCommissionRequest request)
    {
        var result = await _service.CalcCommissionAsync(request);
        return ApiResult<CalcCommissionResultDto>.Success(result);
    }

    /// <summary>
    /// 提交返佣申请到OA审批流程
    /// </summary>
    [HttpPost("submit")]
    [RequirePermission(CrmPermissions.CommissionApply)]
    public async Task<ApiResult<CommissionDto>> Submit([FromBody] SubmitCommissionRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _service.SubmitForApprovalAsync(request, userId);
        return ApiResult<CommissionDto>.Success(result, "返佣申请已提交审批");
    }

    /// <summary>
    /// OA审批回调（审批通过/驳回后更新返佣状态）
    /// </summary>
    [HttpPost("approval-callback")]
    [RequirePermission(CrmPermissions.CommissionApprove)]
    public async Task<ApiResult> ApprovalCallback([FromBody] ApprovalCallbackRequest request)
    {
        await _service.HandleApprovalCallbackAsync(request);
        return ApiResult.Ok("审批回调处理成功");
    }
}

public class UpdateStatusRequest
{
    public int Status { get; set; }
}
