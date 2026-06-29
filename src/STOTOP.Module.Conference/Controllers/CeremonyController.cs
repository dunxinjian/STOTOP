using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Conference.Controllers;

[Authorize]
[ApiController]
[Route("api/conference")]
public class CeremonyController : ControllerBase
{
    private readonly ICeremonyService _ceremonyService;

    public CeremonyController(ICeremonyService ceremonyService)
    {
        _ceremonyService = ceremonyService;
    }

    [HttpGet("events/{eventId}/ceremony")]
    [RequirePermission(ConferencePermissions.CeremonyView)]
    public async Task<ApiResult<List<CeremonyItemDto>>> GetItems(long eventId)
    {
        var result = await _ceremonyService.GetItemsAsync(eventId);
        return ApiResult<List<CeremonyItemDto>>.Success(result);
    }

    [HttpGet("ceremony/{id}")]
    [RequirePermission(ConferencePermissions.CeremonyView)]
    public async Task<ApiResult<CeremonyItemDto>> GetItemById(long id)
    {
        var result = await _ceremonyService.GetItemByIdAsync(id);
        if (result == null)
            return ApiResult<CeremonyItemDto>.Fail("典礼流程项不存在");
        return ApiResult<CeremonyItemDto>.Success(result);
    }

    [HttpPost("events/{eventId}/ceremony")]
    [RequirePermission(ConferencePermissions.CeremonyCreate)]
    public async Task<ApiResult<CeremonyItemDto>> CreateItem(long eventId, [FromBody] CreateCeremonyItemRequest request)
    {
        try
        {
            var result = await _ceremonyService.CreateItemAsync(eventId, request);
            return ApiResult<CeremonyItemDto>.Success(result, "添加流程项成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CeremonyItemDto>.Fail(ex.Message);
        }
    }

    [HttpPut("ceremony/{id}")]
    [RequirePermission(ConferencePermissions.CeremonyEdit)]
    public async Task<ApiResult<CeremonyItemDto>> UpdateItem(long id, [FromBody] UpdateCeremonyItemRequest request)
    {
        try
        {
            var result = await _ceremonyService.UpdateItemAsync(id, request);
            if (result == null)
                return ApiResult<CeremonyItemDto>.Fail("典礼流程项不存在");
            return ApiResult<CeremonyItemDto>.Success(result, "更新流程项成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CeremonyItemDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("ceremony/{id}")]
    [RequirePermission(ConferencePermissions.CeremonyDelete)]
    public async Task<ApiResult> DeleteItem(long id)
    {
        var result = await _ceremonyService.DeleteItemAsync(id);
        if (!result)
            return ApiResult.Fail("典礼流程项不存在");
        return ApiResult.Ok("删除流程项成功");
    }

    [HttpPut("events/{eventId}/ceremony/reorder")]
    [RequirePermission(ConferencePermissions.CeremonyEdit)]
    public async Task<ApiResult> Reorder(long eventId, [FromBody] ReorderCeremonyRequest request)
    {
        var result = await _ceremonyService.ReorderAsync(eventId, request);
        if (!result)
            return ApiResult.Fail("排序失败");
        return ApiResult.Ok("排序更新成功");
    }

    [HttpGet("events/{eventId}/ceremony/export-rundown")]
    [RequirePermission(ConferencePermissions.CeremonyView)]
    public async Task<ApiResult<string>> ExportRundown(long eventId)
    {
        var result = await _ceremonyService.ExportRundownAsync(eventId);
        return ApiResult<string>.Success(result);
    }
}
