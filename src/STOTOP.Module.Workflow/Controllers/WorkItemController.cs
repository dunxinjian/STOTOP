using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Services.Interfaces;
using WorkItemDto = STOTOP.Module.Workflow.DTOs.WorkItemDto;
using WorkItemStatsDto = STOTOP.Module.Workflow.DTOs.WorkItemStatsDto;

namespace STOTOP.Module.Workflow.Controllers;

[Authorize]
[ApiController]
[Route("api/workflow/work-items")]
public class WorkItemController : ControllerBase
{
    private readonly IWorkItemService _workItemService;

    public WorkItemController(IWorkItemService workItemService)
    {
        _workItemService = workItemService;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    [HttpPost]
    public async Task<ApiResult<WorkItemDto>> Create([FromBody] CreateWorkItemRequest request)
    {
        try
        {
            var result = await _workItemService.CreateAsync(request);
            return ApiResult<WorkItemDto>.Success(result, "创建工作项成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<WorkItemDto>.Fail(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<WorkItemDto>> GetById(long id)
    {
        var result = await _workItemService.GetByIdAsync(id);
        if (result == null)
        {
            return ApiResult<WorkItemDto>.Fail("工作项不存在");
        }
        return ApiResult<WorkItemDto>.Success(result);
    }

    [HttpGet("by-uid/{uid}")]
    public async Task<ApiResult<WorkItemDto>> GetByUid(string uid)
    {
        var result = await _workItemService.GetByUidAsync(uid);
        if (result == null)
        {
            return ApiResult<WorkItemDto>.Fail("工作项不存在");
        }
        return ApiResult<WorkItemDto>.Success(result);
    }

    [HttpGet("pending")]
    public async Task<ApiResult<List<WorkItemDto>>> GetPending([FromQuery] string? module)
    {
        var userId = GetUserId();
        var result = await _workItemService.GetPendingItemsAsync(userId, module);
        return ApiResult<List<WorkItemDto>>.Success(result);
    }

    [HttpGet("completed")]
    public async Task<ApiResult<List<WorkItemDto>>> GetCompleted([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        var result = await _workItemService.GetCompletedItemsAsync(userId, page, pageSize);
        return ApiResult<List<WorkItemDto>>.Success(result);
    }

    [HttpGet("by-chain/{chainId}")]
    public async Task<ApiResult<List<WorkItemDto>>> GetByChain(string chainId)
    {
        var result = await _workItemService.GetByChainIdAsync(chainId);
        return ApiResult<List<WorkItemDto>>.Success(result);
    }

    [HttpPost("{id}/assign")]
    public async Task<ApiResult<WorkItemDto>> Assign(long id, [FromBody] AssignRequest request)
    {
        try
        {
            var result = await _workItemService.AssignAsync(id, request.AssigneeId, request.AssigneeName);
            return ApiResult<WorkItemDto>.Success(result, "分配成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<WorkItemDto>.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/start")]
    public async Task<ApiResult<WorkItemDto>> Start(long id)
    {
        try
        {
            var userId = GetUserId();
            var result = await _workItemService.StartAsync(id, userId);
            return ApiResult<WorkItemDto>.Success(result, "开始处理");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<WorkItemDto>.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/complete")]
    public async Task<ApiResult<WorkItemDto>> Complete(long id, [FromBody] CompleteRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _workItemService.CompleteAsync(id, userId, request.Result, request.Remark);
            return ApiResult<WorkItemDto>.Success(result, "处理完成");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<WorkItemDto>.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<ApiResult<WorkItemDto>> Cancel(long id, [FromBody] CancelRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _workItemService.CancelAsync(id, userId, request.Reason);
            return ApiResult<WorkItemDto>.Success(result, "已取消");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<WorkItemDto>.Fail(ex.Message);
        }
    }

    [HttpGet("stats")]
    public async Task<ApiResult<WorkItemStatsDto>> GetStats()
    {
        var userId = GetUserId();
        var result = await _workItemService.GetStatsAsync(userId);
        return ApiResult<WorkItemStatsDto>.Success(result);
    }
}

// Request models for controller actions
public class AssignRequest
{
    public long AssigneeId { get; set; }
    public string AssigneeName { get; set; } = string.Empty;
}

public class CompleteRequest
{
    public string? Result { get; set; }
    public string? Remark { get; set; }
}

public class CancelRequest
{
    public string? Reason { get; set; }
}
