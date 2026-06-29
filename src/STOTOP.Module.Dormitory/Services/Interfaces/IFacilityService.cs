using STOTOP.Module.Dormitory.Dtos;

namespace STOTOP.Module.Dormitory.Services.Interfaces;

public interface IFacilityService
{
    Task<List<FacilityDto>> GetFacilitiesByRoomIdAsync(long roomId);
    Task<FacilityDto?> GetFacilityByIdAsync(long id);
    Task<FacilityDto> CreateFacilityAsync(long roomId, CreateFacilityRequest request);
    Task<FacilityDto?> UpdateFacilityAsync(long id, UpdateFacilityRequest request);
    Task<bool> DeleteFacilityAsync(long id);
}
