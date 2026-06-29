using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;

namespace STOTOP.Module.Dormitory.Services.Interfaces;

public interface IBuildingService
{
    // Building CRUD
    Task<PagedResult<BuildingListItemDto>> GetBuildingsAsync(BuildingQueryRequest request);
    Task<List<BuildingListItemDto>> GetAllEnabledBuildingsAsync();
    Task<BuildingDto?> GetBuildingByIdAsync(long id);
    Task<BuildingDto> CreateBuildingAsync(CreateBuildingRequest request);
    Task<BuildingDto?> UpdateBuildingAsync(long id, UpdateBuildingRequest request);
    Task<bool> DeleteBuildingAsync(long id);
    Task<bool> UpdateStatusAsync(long id, int status);
    Task<bool> CheckCodeExistsAsync(string code, long excludeId);
}
