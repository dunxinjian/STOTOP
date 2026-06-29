using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;

namespace STOTOP.Module.Dormitory.Services.Interfaces;

public interface IRoomService
{
    // Room CRUD
    Task<PagedResult<RoomListItemDto>> GetRoomsAsync(long buildingId, RoomQueryRequest request);
    Task<List<RoomListItemDto>> GetAllEnabledRoomsAsync(long buildingId);
    Task<RoomDto?> GetRoomByIdAsync(long id);
    Task<RoomDto> CreateRoomAsync(long buildingId, CreateRoomRequest request);
    Task<RoomDto?> UpdateRoomAsync(long id, UpdateRoomRequest request);
    Task<bool> DeleteRoomAsync(long id);
    Task<bool> UpdateStatusAsync(long id, int status);
    Task<bool> CheckRoomNumberExistsAsync(long buildingId, string roomNumber, long excludeId);
}
