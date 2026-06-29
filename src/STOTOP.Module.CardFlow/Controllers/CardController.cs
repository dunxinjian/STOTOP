using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/cards")]
public class CardController : ControllerBase
{
    private readonly ICardService _cardService;
    private readonly IFlowEngineService _engine;
    private readonly IAdminAuthorizationService _adminService;

    public CardController(ICardService cardService, IFlowEngineService engine, IAdminAuthorizationService adminService)
    {
        _cardService = cardService;
        _engine = engine;
        _adminService = adminService;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    [HttpGet("available-flows")]
    public async Task<ApiResult<List<AvailableFlowDto>>> GetAvailableFlows([FromQuery] long orgId)
    {
        var result = await _cardService.GetAvailableFlowsAsync(GetUserId(), orgId);
        return ApiResult<List<AvailableFlowDto>>.Success(result);
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<CardListDto>>> GetCards([FromQuery] CardQueryRequest request)
    {
        var result = await _cardService.GetCardsAsync(request);
        return ApiResult<PagedResult<CardListDto>>.Success(result);
    }

    [HttpGet("initiated")]
    public async Task<ApiResult<PagedResult<CardListDto>>> GetInitiated([FromQuery] CardQueryRequest request)
    {
        var result = await _cardService.GetInitiatedCardsAsync(GetUserId(), request);
        return ApiResult<PagedResult<CardListDto>>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<CardDetailDto>> Create([FromBody] CreateCardRequest request)
    {
        try
        {
            var result = await _cardService.CreateAsync(request, GetUserId());
            return ApiResult<CardDetailDto>.Success(result, "创建草稿成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardDetailDto>.Fail(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<CardDetailDto>> GetById(long id)
    {
        var result = await _cardService.GetByIdAsync(id, GetUserId(), _adminService.IsAdmin(User));
        if (result == null)
            return ApiResult<CardDetailDto>.Fail("卡片不存在");
        return ApiResult<CardDetailDto>.Success(result);
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<CardDetailDto>> Update(long id, [FromBody] UpdateCardRequest request)
    {
        try
        {
            var result = await _cardService.UpdateAsync(id, request, GetUserId());
            return ApiResult<CardDetailDto>.Success(result, "更新成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardDetailDto>.Fail(ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResult<CardDetailDto>.Fail("数据已被其他用户修改，请刷新后重试", 409);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        try
        {
            await _cardService.DeleteAsync(id, GetUserId());
            return ApiResult.Ok("删除草稿成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpGet("{id}/available-prerequisites")]
    public async Task<ApiResult<List<CardListDto>>> GetAvailablePrerequisites(long id)
    {
        var result = await _cardService.GetAvailablePrerequisitesAsync(id, GetUserId());
        return ApiResult<List<CardListDto>>.Success(result);
    }

    [HttpGet("{id}/available-offsets")]
    public async Task<ApiResult<List<CardBalanceDto>>> GetAvailableOffsets(long id)
    {
        var result = await _cardService.GetAvailableOffsetsAsync(id, GetUserId());
        return ApiResult<List<CardBalanceDto>>.Success(result);
    }

    [HttpGet("{id}/balance")]
    public async Task<ApiResult<List<CardBalanceDto>>> GetBalance(long id)
    {
        var result = await _cardService.GetBalanceAsync(id);
        return ApiResult<List<CardBalanceDto>>.Success(result);
    }

    [HttpPost("{id}/relations")]
    public async Task<ApiResult<CardRelationDto>> CreateRelation(long id, [FromBody] CreateRelationRequest request)
    {
        try
        {
            var result = await _cardService.CreateRelationAsync(id, request, GetUserId());
            return ApiResult<CardRelationDto>.Success(result, "关联建立成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardRelationDto>.Fail(ex.Message);
        }
    }

    [HttpGet("{id}/relations")]
    public async Task<ApiResult<List<CardRelationDto>>> GetRelations(long id)
    {
        var result = await _cardService.GetRelationsAsync(id);
        return ApiResult<List<CardRelationDto>>.Success(result);
    }

    [HttpGet("{id}/relations/{relationId}/snapshot")]
    public async Task<ApiResult<string>> GetRelationSnapshot(long id, long relationId)
    {
        var result = await _cardService.GetRelationSnapshotAsync(id, relationId);
        if (result == null)
            return ApiResult<string>.Fail("快照不存在");
        return ApiResult<string>.Success(result);
    }

    [HttpPost("{id}/submit")]
    public async Task<ApiResult<CardOperationResult>> Submit(long id)
    {
        try
        {
            var result = await _engine.SubmitAsync(id, GetUserId());
            if (!result.Success)
                return ApiResult<CardOperationResult>.Fail(result.Message ?? "提交失败");
            return ApiResult<CardOperationResult>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardOperationResult>.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/approve")]
    public async Task<ApiResult<CardOperationResult>> Approve(long id, [FromBody] ApproveRequest request)
    {
        try
        {
            var result = await _engine.ApproveAsync(id, GetUserId(), request);
            if (!result.Success)
                return ApiResult<CardOperationResult>.Fail(result.Message ?? "审批失败");
            return ApiResult<CardOperationResult>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardOperationResult>.Fail(ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResult<CardOperationResult>.Fail("数据已被其他用户修改，请刷新后重试", 409);
        }
    }

    [HttpPost("{id}/reject")]
    public async Task<ApiResult<CardOperationResult>> Reject(long id, [FromBody] RejectRequest request)
    {
        try
        {
            var result = await _engine.RejectAsync(id, GetUserId(), request);
            if (!result.Success)
                return ApiResult<CardOperationResult>.Fail(result.Message ?? "退回失败");
            return ApiResult<CardOperationResult>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardOperationResult>.Fail(ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResult<CardOperationResult>.Fail("数据已被其他用户修改，请刷新后重试", 409);
        }
    }

    [HttpPost("{id}/withdraw")]
    public async Task<ApiResult<CardOperationResult>> Withdraw(long id)
    {
        try
        {
            var result = await _engine.WithdrawAsync(id, GetUserId());
            if (!result.Success)
                return ApiResult<CardOperationResult>.Fail(result.Message ?? "撤回失败");
            return ApiResult<CardOperationResult>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardOperationResult>.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/resubmit")]
    public async Task<ApiResult<CardOperationResult>> Resubmit(long id)
    {
        try
        {
            var result = await _engine.ResubmitAsync(id, GetUserId());
            if (!result.Success)
                return ApiResult<CardOperationResult>.Fail(result.Message ?? "重新提交失败");
            return ApiResult<CardOperationResult>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardOperationResult>.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/void")]
    public async Task<ApiResult<CardOperationResult>> Void(long id, [FromBody] VoidRequest? request = null)
    {
        try
        {
            var result = await _engine.VoidAsync(id, GetUserId(), request?.Opinion);
            if (!result.Success)
                return ApiResult<CardOperationResult>.Fail(result.Message ?? "废除失败");
            return ApiResult<CardOperationResult>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardOperationResult>.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/resume")]
    public async Task<ApiResult<CardOperationResult>> Resume(long id)
    {
        try
        {
            var result = await _engine.ResumeAsync(id, GetUserId());
            if (!result.Success)
                return ApiResult<CardOperationResult>.Fail(result.Message ?? "恢复失败");
            return ApiResult<CardOperationResult>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardOperationResult>.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/countersign")]
    public async Task<ApiResult<CardOperationResult>> Countersign(long id, [FromBody] CountersignRequest request)
    {
        try
        {
            var result = await _engine.CountersignAsync(id, GetUserId(), request);
            if (!result.Success)
                return ApiResult<CardOperationResult>.Fail(result.Message ?? "加签失败");
            return ApiResult<CardOperationResult>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardOperationResult>.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/transfer")]
    public async Task<ApiResult<CardOperationResult>> Transfer(long id, [FromBody] TransferRequest request)
    {
        try
        {
            var result = await _engine.TransferAsync(id, GetUserId(), request);
            if (!result.Success)
                return ApiResult<CardOperationResult>.Fail(result.Message ?? "转交失败");
            return ApiResult<CardOperationResult>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardOperationResult>.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/cc")]
    public async Task<ApiResult<CardOperationResult>> Cc(long id, [FromBody] CcRequest request)
    {
        try
        {
            var result = await _engine.CcAsync(id, GetUserId(), request);
            if (!result.Success)
                return ApiResult<CardOperationResult>.Fail(result.Message ?? "抄送失败");
            return ApiResult<CardOperationResult>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardOperationResult>.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/urge")]
    public async Task<ApiResult<CardOperationResult>> Urge(long id, [FromBody] UrgeRequest? request = null)
    {
        try
        {
            var result = await _engine.UrgeAsync(id, GetUserId(), request?.Message);
            if (!result.Success)
                return ApiResult<CardOperationResult>.Fail(result.Message ?? "催办失败");
            return ApiResult<CardOperationResult>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardOperationResult>.Fail(ex.Message);
        }
    }

    [HttpGet("{id}/logs")]
    public async Task<ApiResult<List<ActionLogDto>>> GetLogs(long id)
    {
        var result = await _cardService.GetLogsAsync(id);
        return ApiResult<List<ActionLogDto>>.Success(result);
    }

    /// <summary>全局审计日志查询（分页、多条件）</summary>
    [HttpGet("~/api/cardflow/audit/logs")]
    public async Task<ApiResult<PagedResult<AuditLogItemDto>>> SearchAuditLogs([FromQuery] AuditLogQueryRequest request)
    {
        var result = await _cardService.SearchLogsAsync(request);
        return ApiResult<PagedResult<AuditLogItemDto>>.Success(result);
    }

    /// <summary>CardFlow 条件路由和动态审批运行监控聚合</summary>
    [HttpGet("~/api/cardflow/audit/runtime-monitoring")]
    public async Task<ApiResult<CardFlowRuntimeMonitoringDto>> GetRuntimeMonitoring([FromQuery] CardFlowRuntimeMonitoringRequest request)
    {
        var result = await _cardService.GetRuntimeMonitoringAsync(request);
        return ApiResult<CardFlowRuntimeMonitoringDto>.Success(result);
    }

    [HttpPost("{id}/retry-push")]
    public async Task<ApiResult> RetryPush(long id)
    {
        try
        {
            await _cardService.RetryPushAsync(id, GetUserId());
            return ApiResult.Ok("重推成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
