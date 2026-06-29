using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;

namespace STOTOP.Module.Dormitory.Services.Interfaces;

public interface IBedService
{
    // Bed CRUD
    Task<PagedResult<BedListItemDto>> GetBedsAsync(long roomId, BedQueryRequest request);
    Task<List<BedListItemDto>> GetAllEnabledBedsAsync(long roomId);
    Task<BedDto?> GetBedByIdAsync(long id);
    Task<BedDto> CreateBedAsync(long roomId, CreateBedRequest request);
    Task<BedDto?> UpdateBedAsync(long id, UpdateBedRequest request);
    Task<bool> DeleteBedAsync(long id);
    Task<bool> UpdateStatusAsync(long id, int status);
    Task<bool> CheckBedNumberExistsAsync(long roomId, string bedNumber, long excludeId);
}
