using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.Workflow.Controllers;

[Authorize]
[ApiController]
[Route("api/workflow/chains")]
public class ChainController : ControllerBase
{
    private readonly IChainService _chainService;

    public ChainController(IChainService chainService)
    {
        _chainService = chainService;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private string GetUserName() => User.Identity?.Name ?? "";

    [HttpGet("{chainId}/timeline")]
    public async Task<ApiResult<ChainTimelineDto>> GetTimeline(string chainId)
    {
        var result = await _chainService.GetTimelineAsync(chainId);
        return ApiResult<ChainTimelineDto>.Success(result);
    }

    [HttpPost("{chainId}/comments")]
    public async Task<ApiResult<ChainCommentDto>> AddComment(string chainId, [FromBody] AddCommentRequest request)
    {
        try
        {
            var userId = GetUserId();
            var userName = GetUserName();
            var result = await _chainService.AddCommentAsync(chainId, userId, userName, request.Content, request.WorkItemId, request.ReplyToId);
            return ApiResult<ChainCommentDto>.Success(result, "评论发布成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ChainCommentDto>.Fail(ex.Message);
        }
    }

    [HttpGet("{chainId}/comments")]
    public async Task<ApiResult<List<ChainCommentDto>>> GetComments(string chainId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _chainService.GetCommentsAsync(chainId, page, pageSize);
        return ApiResult<List<ChainCommentDto>>.Success(result);
    }

    [HttpDelete("comments/{commentId}")]
    public async Task<ApiResult> DeleteComment(long commentId)
    {
        try
        {
            var userId = GetUserId();
            await _chainService.DeleteCommentAsync(commentId, userId);
            return ApiResult.Ok("评论已删除");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("{chainId}/follow")]
    public async Task<ApiResult> Follow(string chainId)
    {
        try
        {
            var userId = GetUserId();
            var userName = GetUserName();
            await _chainService.FollowAsync(chainId, userId, userName);
            return ApiResult.Ok("关注成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpDelete("{chainId}/follow")]
    public async Task<ApiResult> Unfollow(string chainId)
    {
        try
        {
            var userId = GetUserId();
            await _chainService.UnfollowAsync(chainId, userId);
            return ApiResult.Ok("已取消关注");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpGet("{chainId}/followers")]
    public async Task<ApiResult<List<ChainFollowerDto>>> GetFollowers(string chainId)
    {
        var result = await _chainService.GetFollowersAsync(chainId);
        return ApiResult<List<ChainFollowerDto>>.Success(result);
    }

    [HttpGet("{chainId}/is-following")]
    public async Task<ApiResult<bool>> IsFollowing(string chainId)
    {
        var userId = GetUserId();
        var result = await _chainService.IsFollowingAsync(chainId, userId);
        return ApiResult<bool>.Success(result);
    }
}

public class AddCommentRequest
{
    public string Content { get; set; } = string.Empty;
    public long? WorkItemId { get; set; }
    public long? ReplyToId { get; set; }
}
