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
public class AccommodationController : ControllerBase
{
    private readonly IAccommodationService _accommodationService;

    public AccommodationController(IAccommodationService accommodationService)
    {
        _accommodationService = accommodationService;
    }

    // === Hotel CRUD ===

    [HttpGet("events/{eventId}/hotels")]
    [RequirePermission(ConferencePermissions.AccommodationView)]
    public async Task<ApiResult<List<HotelListItemDto>>> GetHotels(int eventId)
    {
        var result = await _accommodationService.GetHotelsAsync(eventId);
        return ApiResult<List<HotelListItemDto>>.Success(result);
    }

    [HttpPost("events/{eventId}/hotels")]
    [RequirePermission(ConferencePermissions.AccommodationCreate)]
    public async Task<ApiResult<HotelDto>> CreateHotel(int eventId, [FromBody] CreateHotelRequest request)
    {
        try
        {
            var result = await _accommodationService.CreateHotelAsync(eventId, request);
            return ApiResult<HotelDto>.Success(result, "添加酒店成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<HotelDto>.Fail(ex.Message);
        }
    }

    [HttpPut("hotels/{id}")]
    [RequirePermission(ConferencePermissions.AccommodationEdit)]
    public async Task<ApiResult<HotelDto>> UpdateHotel(int id, [FromBody] UpdateHotelRequest request)
    {
        try
        {
            var result = await _accommodationService.UpdateHotelAsync(id, request);
            if (result == null)
                return ApiResult<HotelDto>.Fail("酒店不存在");
            return ApiResult<HotelDto>.Success(result, "更新酒店成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<HotelDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("hotels/{id}")]
    [RequirePermission(ConferencePermissions.AccommodationDelete)]
    public async Task<ApiResult> DeleteHotel(int id)
    {
        var result = await _accommodationService.DeleteHotelAsync(id);
        if (!result)
            return ApiResult.Fail("酒店不存在");
        return ApiResult.Ok("删除酒店成功");
    }

    // === Room CRUD ===

    [HttpGet("hotels/{hotelId}/rooms")]
    [RequirePermission(ConferencePermissions.AccommodationView)]
    public async Task<ApiResult<List<RoomListItemDto>>> GetRooms(int hotelId)
    {
        var result = await _accommodationService.GetRoomsAsync(hotelId);
        return ApiResult<List<RoomListItemDto>>.Success(result);
    }

    [HttpPost("hotels/{hotelId}/rooms/batch")]
    [RequirePermission(ConferencePermissions.AccommodationCreate)]
    public async Task<ApiResult<List<RoomDto>>> BatchAddRooms(int hotelId, [FromBody] BatchAddRoomRequest request)
    {
        try
        {
            var result = await _accommodationService.BatchAddRoomsAsync(hotelId, request);
            return ApiResult<List<RoomDto>>.Success(result, "批量添加房间成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<List<RoomDto>>.Fail(ex.Message);
        }
    }

    [HttpPut("rooms/{id}")]
    [RequirePermission(ConferencePermissions.AccommodationEdit)]
    public async Task<ApiResult<RoomDto>> UpdateRoom(int id, [FromBody] UpdateRoomRequest request)
    {
        try
        {
            var result = await _accommodationService.UpdateRoomAsync(id, request);
            if (result == null)
                return ApiResult<RoomDto>.Fail("房间不存在");
            return ApiResult<RoomDto>.Success(result, "更新房间成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<RoomDto>.Fail(ex.Message);
        }
    }

    [HttpPut("rooms/{id}/assign")]
    [RequirePermission(ConferencePermissions.AccommodationEdit)]
    public async Task<ApiResult> AssignRoom(int id, [FromBody] RoomAssignRequest request)
    {
        var result = await _accommodationService.AssignRoomAsync(id, request);
        if (!result)
            return ApiResult.Fail("分配失败");
        return ApiResult.Ok("分配房间成功");
    }

    // === Smart Algorithms ===

    [HttpPost("events/{eventId}/accommodation/auto-assign")]
    [RequirePermission(ConferencePermissions.AccommodationEdit)]
    public async Task<ApiResult<AutoAssignPreviewDto>> AutoAssign(int eventId)
    {
        var result = await _accommodationService.AutoAssignAsync(eventId);
        return ApiResult<AutoAssignPreviewDto>.Success(result);
    }

    [HttpGet("events/{eventId}/accommodation/unassigned")]
    [RequirePermission(ConferencePermissions.AccommodationView)]
    public async Task<ApiResult<List<AttendeeListItemDto>>> GetUnassigned(int eventId)
    {
        var result = await _accommodationService.GetUnassignedAsync(eventId);
        return ApiResult<List<AttendeeListItemDto>>.Success(result);
    }

    // === Export ===

    [HttpGet("events/{eventId}/accommodation/demand-stats")]
    [RequirePermission(ConferencePermissions.AccommodationView)]
    public async Task<ApiResult<AccommodationDemandStatsDto>> GetDemandStats(int eventId)
    {
        var result = await _accommodationService.GetDemandStatsAsync(eventId);
        return ApiResult<AccommodationDemandStatsDto>.Success(result);
    }

    [HttpGet("events/{eventId}/accommodation/room-type-guests")]
    [RequirePermission(ConferencePermissions.AccommodationView)]
    public async Task<ApiResult<List<RoomTypeGuestDto>>> GetRoomTypeGuests(int eventId, [FromQuery] DateTime date, [FromQuery] string roomType)
    {
        var guests = await _accommodationService.GetRoomTypeGuestsAsync(eventId, date, roomType);
        return ApiResult<List<RoomTypeGuestDto>>.Success(guests);
    }

    [HttpGet("events/{eventId}/accommodation/export-pdf")]
    [RequirePermission(ConferencePermissions.AccommodationView)]
    public async Task<IActionResult> ExportPdf(int eventId)
    {
        var bytes = await _accommodationService.ExportPdfAsync(eventId);
        return File(bytes, "application/pdf", "住宿安排.pdf");
    }

    [HttpGet("events/{eventId}/accommodation/demand-stats/export-excel")]
    [RequirePermission(ConferencePermissions.AccommodationView)]
    public async Task<IActionResult> ExportDemandStatsExcel(int eventId)
    {
        var bytes = await _accommodationService.ExportDemandStatsExcelAsync(eventId);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "住宿需求统计.xlsx");
    }
}
