using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Points.Controllers;

[Authorize]
[ApiController]
[Route("api/points/applications")]
public class PointApplicationController : ControllerBase
{
    private readonly IPointApplicationService _service;

    public PointApplicationController(IPointApplicationService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>提交申请</summary>
    [HttpPost]
    [RequirePermission(PointsPermissions.ApplicationSubmit)]
    public async Task<ApiResult<PointApplicationDetailDto>> Submit([FromBody] SubmitPointApplicationRequest request)
    {
        return await _service.SubmitAsync(GetOrgId(), GetUserId(), request);
    }

    /// <summary>申请列表</summary>
    [HttpGet]
    [RequirePermission(PointsPermissions.ApplicationSubmit)]
    public async Task<ApiResult<PagedResult<PointApplicationListDto>>> GetPagedList([FromQuery] ApplicationPagedRequest request)
    {
        return await _service.GetPagedListAsync(GetOrgId(), request);
    }

    /// <summary>我的申请</summary>
    [HttpGet("my")]
    [RequirePermission(PointsPermissions.ApplicationSubmit)]
    public async Task<ApiResult<PagedResult<PointApplicationListDto>>> GetMyApplications([FromQuery] MyApplicationPagedRequest request)
    {
        return await _service.GetMyApplicationsAsync(GetOrgId(), GetUserId(), request);
    }

    /// <summary>待审批列表</summary>
    [HttpGet("pending")]
    [RequirePermission(PointsPermissions.ApplicationApprove)]
    public async Task<ApiResult<PagedResult<PointApplicationListDto>>> GetPending([FromQuery] PendingApplicationPagedRequest request)
    {
        return await _service.GetPendingAsync(GetOrgId(), request);
    }

    /// <summary>审批通过</summary>
    [HttpPut("{id}/approve")]
    [RequirePermission(PointsPermissions.ApplicationApprove)]
    public async Task<ApiResult<bool>> Approve(long id, [FromBody] ApprovePointApplicationRequest request)
    {
        return await _service.ApproveAsync(id, GetUserId(), request);
    }

    /// <summary>审批拒绝</summary>
    [HttpPut("{id}/reject")]
    [RequirePermission(PointsPermissions.ApplicationApprove)]
    public async Task<ApiResult<bool>> Reject(long id, [FromBody] string reason)
    {
        return await _service.RejectAsync(id, GetUserId(), reason);
    }
}
