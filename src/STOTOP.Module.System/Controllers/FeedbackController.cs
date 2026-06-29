using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Filters;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

[Authorize]
[ApiController]
[Route("api/system/feedback")]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _service;

    public FeedbackController(IFeedbackService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<FeedbackCardDto>>> GetPaged([FromQuery] FeedbackQueryRequest request)
    {
        return ApiResult<PagedResult<FeedbackCardDto>>.Success(await _service.GetPagedAsync(request, GetUserId()));
    }

    [HttpGet("board")]
    public async Task<ApiResult<List<FeedbackCardDto>>> GetBoard([FromQuery] FeedbackQueryRequest request)
    {
        return ApiResult<List<FeedbackCardDto>>.Success(await _service.GetBoardAsync(request, GetUserId()));
    }

    [HttpGet("counts")]
    public async Task<ApiResult<List<FeedbackStatusCountDto>>> GetCounts([FromQuery] FeedbackQueryRequest request)
    {
        return ApiResult<List<FeedbackStatusCountDto>>.Success(await _service.GetStatusCountsAsync(request, GetUserId()));
    }

    [HttpGet("{id:long}")]
    public async Task<ApiResult<FeedbackDetailDto>> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        return result == null
            ? ApiResult<FeedbackDetailDto>.Fail("反馈不存在", 404)
            : ApiResult<FeedbackDetailDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<FeedbackDetailDto>> Create([FromBody] CreateFeedbackRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request, GetUserId(), GetOrgId());
            return ApiResult<FeedbackDetailDto>.Success(result, "反馈已提交");
        }
        catch (Exception ex)
        {
            return ApiResult<FeedbackDetailDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id:long}")]
    [RequirePermission(SystemPermissions.FeedbackManage)]
    public async Task<ApiResult<FeedbackDetailDto>> Update(long id, [FromBody] UpdateFeedbackRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request, GetUserId());
            return result == null
                ? ApiResult<FeedbackDetailDto>.Fail("反馈不存在", 404)
                : ApiResult<FeedbackDetailDto>.Success(result, "反馈已更新");
        }
        catch (Exception ex)
        {
            return ApiResult<FeedbackDetailDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id:long}/assign")]
    [RequirePermission(SystemPermissions.FeedbackManage)]
    public async Task<ApiResult<FeedbackDetailDto>> Assign(long id, [FromBody] AssignFeedbackRequest request)
    {
        var result = await _service.AssignAsync(id, request, GetUserId());
        return result == null
            ? ApiResult<FeedbackDetailDto>.Fail("反馈不存在", 404)
            : ApiResult<FeedbackDetailDto>.Success(result, "反馈已分派");
    }

    [HttpPut("{id:long}/transition")]
    [RequirePermission(SystemPermissions.FeedbackManage)]
    public async Task<ApiResult<FeedbackDetailDto>> Transition(long id, [FromBody] TransitionFeedbackRequest request)
    {
        try
        {
            var result = await _service.TransitionAsync(id, request, GetUserId());
            return result == null
                ? ApiResult<FeedbackDetailDto>.Fail("反馈不存在", 404)
                : ApiResult<FeedbackDetailDto>.Success(result, "状态已更新");
        }
        catch (Exception ex)
        {
            return ApiResult<FeedbackDetailDto>.Fail(ex.Message);
        }
    }

    [HttpPost("{id:long}/comments")]
    public async Task<ApiResult<FeedbackActivityDto>> AddComment(long id, [FromBody] AddFeedbackCommentRequest request)
    {
        try
        {
            var result = await _service.AddCommentAsync(id, request, GetUserId());
            return result == null
                ? ApiResult<FeedbackActivityDto>.Fail("反馈不存在", 404)
                : ApiResult<FeedbackActivityDto>.Success(result, "评论已添加");
        }
        catch (Exception ex)
        {
            return ApiResult<FeedbackActivityDto>.Fail(ex.Message);
        }
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
}
