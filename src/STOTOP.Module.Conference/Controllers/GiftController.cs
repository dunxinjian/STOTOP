using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Conference.Controllers;

[Authorize]
[ApiController]
[Route("api/conference")]
public class GiftController : ControllerBase
{
    private readonly IGiftService _giftService;

    public GiftController(IGiftService giftService)
    {
        _giftService = giftService;
    }

    [HttpGet("events/{eventId}/gifts")]
    [RequirePermission(ConferencePermissions.GiftView)]
    public async Task<ApiResult<List<GiftDto>>> GetGifts(long eventId)
    {
        var result = await _giftService.GetGiftsAsync(eventId);
        return ApiResult<List<GiftDto>>.Success(result);
    }

    [HttpGet("gifts/{id}")]
    [RequirePermission(ConferencePermissions.GiftView)]
    public async Task<ApiResult<GiftDto>> GetGiftById(long id)
    {
        var result = await _giftService.GetGiftByIdAsync(id);
        if (result == null)
            return ApiResult<GiftDto>.Fail("礼金记录不存在");
        return ApiResult<GiftDto>.Success(result);
    }

    [HttpPost("events/{eventId}/gifts")]
    [RequirePermission(ConferencePermissions.GiftCreate)]
    public async Task<ApiResult<GiftDto>> CreateGift(long eventId, [FromBody] CreateGiftRequest request)
    {
        try
        {
            var result = await _giftService.CreateGiftAsync(eventId, request);
            return ApiResult<GiftDto>.Success(result, "登记礼金成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<GiftDto>.Fail(ex.Message);
        }
        catch (DbUpdateException ex)
        {
            return ApiResult<GiftDto>.Fail($"数据库保存失败：{ex.InnerException?.Message ?? ex.Message}");
        }
    }

    [HttpPut("gifts/{id}")]
    [RequirePermission(ConferencePermissions.GiftEdit)]
    public async Task<ApiResult<GiftDto>> UpdateGift(long id, [FromBody] UpdateGiftRequest request)
    {
        try
        {
            var result = await _giftService.UpdateGiftAsync(id, request);
            if (result == null)
                return ApiResult<GiftDto>.Fail("礼金记录不存在");
            return ApiResult<GiftDto>.Success(result, "更新礼金成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<GiftDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("gifts/{id}")]
    [RequirePermission(ConferencePermissions.GiftDelete)]
    public async Task<ApiResult> DeleteGift(long id)
    {
        var result = await _giftService.DeleteGiftAsync(id);
        if (!result)
            return ApiResult.Fail("礼金记录不存在");
        return ApiResult.Ok("删除礼金成功");
    }

    [HttpGet("events/{eventId}/gifts/summary")]
    [RequirePermission(ConferencePermissions.GiftView)]
    public async Task<ApiResult<GiftSummaryDto>> GetSummary(long eventId)
    {
        var result = await _giftService.GetSummaryAsync(eventId);
        return ApiResult<GiftSummaryDto>.Success(result);
    }

    [HttpPost("events/{eventId}/gifts/batch")]
    [RequirePermission(ConferencePermissions.GiftCreate)]
    public async Task<ApiResult<int>> BatchRegister(long eventId, [FromBody] BatchRegisterGiftRequest request)
    {
        try
        {
            var count = await _giftService.BatchRegisterAsync(eventId, request);
            return ApiResult<int>.Success(count, $"批量登记成功，共 {count} 条");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<int>.Fail(ex.Message);
        }
    }

    [HttpGet("events/{eventId}/gifts/export")]
    [RequirePermission(ConferencePermissions.GiftExport)]
    public async Task<IActionResult> ExportGifts(long eventId)
    {
        var bytes = await _giftService.ExportGiftsAsync(eventId);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "礼金登记.xlsx");
    }
}
