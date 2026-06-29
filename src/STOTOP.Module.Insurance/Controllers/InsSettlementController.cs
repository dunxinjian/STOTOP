using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Insurance.Dtos;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Controllers;

/// <summary>
/// 理赔管理控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/insurance/settlements")]
public class InsSettlementController : ControllerBase
{
    private readonly ISettlementService _service;

    public InsSettlementController(ISettlementService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取理赔列表（分页）
    /// </summary>
    [HttpGet]
    [RequirePermission(InsurancePermissions.SettlementView)]
    public async Task<ApiResult<PagedResult<InsSettlementListItemDto>>> GetList([FromQuery] InsSettlementQueryRequest request)
    {
        var result = await _service.GetListAsync(request);
        return ApiResult<PagedResult<InsSettlementListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取理赔详情
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(InsurancePermissions.SettlementView)]
    public async Task<ApiResult<InsSettlementDto>> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
        {
            return ApiResult<InsSettlementDto>.Fail("理赔记录不存在");
        }
        return ApiResult<InsSettlementDto>.Success(result);
    }

    /// <summary>
    /// 创建理赔记录
    /// </summary>
    [HttpPost]
    [RequirePermission(InsurancePermissions.SettlementCreate)]
    public async Task<ApiResult<InsSettlementDto>> Create([FromBody] CreateInsSettlementRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return ApiResult<InsSettlementDto>.Success(result, "创建理赔记录成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<InsSettlementDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新理赔记录
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(InsurancePermissions.SettlementCreate)]
    public async Task<ApiResult<InsSettlementDto>> Update(long id, [FromBody] UpdateInsSettlementRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request);
            if (result == null)
            {
                return ApiResult<InsSettlementDto>.Fail("理赔记录不存在");
            }
            return ApiResult<InsSettlementDto>.Success(result, "更新理赔记录成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<InsSettlementDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 提交理赔审批
    /// </summary>
    [HttpPut("{id}/submit")]
    [RequirePermission(InsurancePermissions.SettlementSubmit)]
    public async Task<ApiResult> Submit(long id)
    {
        try
        {
            await _service.SubmitAsync(id);
            return ApiResult.Ok("提交审批成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 审核理赔
    /// </summary>
    [HttpPut("{id}/review")]
    [RequirePermission(InsurancePermissions.SettlementReview)]
    public async Task<ApiResult> Review(long id, [FromBody] CreateInsApprovalRecordRequest request)
    {
        try
        {
            request.SettlementId = id;
            await _service.ReviewAsync(request);
            return ApiResult.Ok("审核操作成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取待我审批的理赔列表
    /// </summary>
    [HttpGet("pending-my")]
    [RequirePermission(InsurancePermissions.SettlementView)]
    public async Task<ApiResult<PagedResult<InsSettlementListItemDto>>> GetPendingMy([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20)
    {
        var approverId = GetCurrentUserId();
        var result = await _service.GetPendingMyAsync(approverId, pageIndex, pageSize);
        return ApiResult<PagedResult<InsSettlementListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取理赔审批历史
    /// </summary>
    [HttpGet("{id}/approval-history")]
    [RequirePermission(InsurancePermissions.SettlementView)]
    public async Task<ApiResult<List<InsApprovalRecordListItemDto>>> GetApprovalHistory(long id)
    {
        var result = await _service.GetApprovalHistoryAsync(id);
        return ApiResult<List<InsApprovalRecordListItemDto>>.Success(result);
    }

    private long GetCurrentUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (long.TryParse(claim, out var userId)) return userId;
        var userIdObj = HttpContext.Items["CurrentUserId"];
        return userIdObj is long id ? id : 0;
    }

    /// <summary>
    /// 审批通过理赔
    /// </summary>
    [HttpPut("{id}/approve")]
    [RequirePermission(InsurancePermissions.SettlementApprove)]
    public async Task<ApiResult> Approve(long id, [FromBody] ApproveSettlementRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var reviewRequest = new CreateInsApprovalRecordRequest
            {
                SettlementId = id,
                ApprovalAction = 1, // 通过
                ApprovalComment = request.ApprovalComment
            };
            await _service.ReviewAsync(reviewRequest);
            return ApiResult.Ok("审批通过成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 拨付理赔
    /// </summary>
    [HttpPut("{id}/pay")]
    [RequirePermission(InsurancePermissions.SettlementPay)]
    public async Task<ApiResult> Pay(long id, [FromBody] PaySettlementRequest request)
    {
        try
        {
            await _service.PayAsync(id, request.PaymentVoucher);
            return ApiResult.Ok("拨付成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}

/// <summary>
/// 审批理赔请求 DTO
/// </summary>
public class ApproveSettlementRequest
{
    public string? ApprovalComment { get; set; }
}

/// <summary>
/// 拨付理赔请求 DTO
/// </summary>
public class PaySettlementRequest
{
    public string? PaymentVoucher { get; set; }
}
