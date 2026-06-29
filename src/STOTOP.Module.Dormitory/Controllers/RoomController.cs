using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Controllers;

[Authorize]
[ApiController]
[Route("api/dormitory/buildings/{buildingId}/rooms")]
public class RoomController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<RoomListItemDto>>> GetList(long buildingId, [FromQuery] RoomQueryRequest request)
    {
        var result = await _roomService.GetRoomsAsync(buildingId, request);
        return ApiResult<PagedResult<RoomListItemDto>>.Success(result);
    }

    [HttpGet("all")]
    public async Task<ApiResult<List<RoomListItemDto>>> GetAllEnabled(long buildingId)
    {
        var result = await _roomService.GetAllEnabledRoomsAsync(buildingId);
        return ApiResult<List<RoomListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<RoomDto>> GetById(long buildingId, long id)
    {
        var result = await _roomService.GetRoomByIdAsync(id);
        if (result == null)
        {
            return ApiResult<RoomDto>.Fail("房间不存在");
        }
        return ApiResult<RoomDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<RoomDto>> Create(long buildingId, [FromBody] CreateRoomRequest request)
    {
        try
        {
            var result = await _roomService.CreateRoomAsync(buildingId, request);
            return ApiResult<RoomDto>.Success(result, "创建房间成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<RoomDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<RoomDto>> Update(long buildingId, long id, [FromBody] UpdateRoomRequest request)
    {
        try
        {
            var result = await _roomService.UpdateRoomAsync(id, request);
            if (result == null)
            {
                return ApiResult<RoomDto>.Fail("房间不存在");
            }
            return ApiResult<RoomDto>.Success(result, "更新房间成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<RoomDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long buildingId, long id)
    {
        var result = await _roomService.DeleteRoomAsync(id);
        if (!result)
        {
            return ApiResult.Fail("房间不存在");
        }
        return ApiResult.Ok("删除房间成功");
    }

    [HttpPut("{id}/status")]
    public async Task<ApiResult> UpdateStatus(long buildingId, long id, [FromBody] UpdateRoomStatusRequest request)
    {
        var result = await _roomService.UpdateStatusAsync(id, request.Status);
        if (!result)
        {
            return ApiResult.Fail("房间不存在");
        }
        return ApiResult.Ok("更新状态成功");
    }

    [HttpGet("check-number")]
    public async Task<ApiResult<bool>> CheckNumber(long buildingId, [FromQuery] string roomNumber, [FromQuery] long excludeId = 0)
    {
        var exists = await _roomService.CheckRoomNumberExistsAsync(buildingId, roomNumber, excludeId);
        return ApiResult<bool>.Success(exists);
    }
}
