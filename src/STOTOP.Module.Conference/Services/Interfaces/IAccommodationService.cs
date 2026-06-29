using STOTOP.Module.Conference.Dtos;

namespace STOTOP.Module.Conference.Services.Interfaces;

public interface IAccommodationService
{
    // Hotel CRUD
    Task<List<HotelListItemDto>> GetHotelsAsync(int eventId);
    Task<HotelDto> CreateHotelAsync(int eventId, CreateHotelRequest request);
    Task<HotelDto?> UpdateHotelAsync(int id, UpdateHotelRequest request);
    Task<bool> DeleteHotelAsync(int id);

    // Room CRUD
    Task<List<RoomListItemDto>> GetRoomsAsync(int hotelId);
    Task<List<RoomDto>> BatchAddRoomsAsync(int hotelId, BatchAddRoomRequest request);
    Task<RoomDto?> UpdateRoomAsync(int id, UpdateRoomRequest request);
    Task<bool> AssignRoomAsync(int id, RoomAssignRequest request);

    // Smart algorithms
    Task<AutoAssignPreviewDto> AutoAssignAsync(int eventId);
    Task<List<AttendeeListItemDto>> GetUnassignedAsync(int eventId);

    // Statistics
    Task<AccommodationDemandStatsDto> GetDemandStatsAsync(int eventId);
    Task<List<RoomTypeGuestDto>> GetRoomTypeGuestsAsync(int eventId, DateTime date, string roomType);

    // Export
    Task<byte[]> ExportPdfAsync(int eventId);
    Task<byte[]> ExportDemandStatsExcelAsync(int eventId);
}
