using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.Workflow.Controllers;

[Authorize]
[ApiController]
[Route("api/workflow/issues")]
public class IssuePackController : ControllerBase
{
    private readonly IIssueAggregator _issueAggregator;

    public IssuePackController(IIssueAggregator issueAggregator)
    {
        _issueAggregator = issueAggregator;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    [HttpGet("packs/{id}")]
    public async Task<ApiResult<IssuePackDto>> GetPack(long id)
    {
        var result = await _issueAggregator.GetIssuePackAsync(id);
        if (result == null)
        {
            return ApiResult<IssuePackDto>.Fail("问题包不存在");
        }
        return ApiResult<IssuePackDto>.Success(result);
    }

    [HttpGet("packs/by-chain/{chainId}")]
    public async Task<ApiResult<List<IssuePackDto>>> GetPacksByChain(string chainId)
    {
        var result = await _issueAggregator.GetIssuePacksByChainAsync(chainId);
        return ApiResult<List<IssuePackDto>>.Success(result);
    }

    [HttpPost("details/{id}/resolve")]
    public async Task<ApiResult> ResolveDetail(long id, [FromBody] ResolveIssueRequest request)
    {
        try
        {
            var userId = GetUserId();
            await _issueAggregator.ResolveIssueAsync(id, userId, request.CorrectedValue);
            return ApiResult.Ok("问题已解决");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("packs/{id}/batch-resolve")]
    public async Task<ApiResult> BatchResolve(long id)
    {
        try
        {
            var userId = GetUserId();
            await _issueAggregator.BatchResolveAsync(id, userId);
            return ApiResult.Ok("批量解决成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}

public class ResolveIssueRequest
{
    public string? CorrectedValue { get; set; }
}
