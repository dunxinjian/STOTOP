using STOTOP.Core.Models;
using STOTOP.Module.Vehicle.Dtos;

namespace STOTOP.Module.Vehicle.Services.Interfaces;

public interface IRentalStandardService
{
    Task<PagedResult<RentalStandardListItemDto>> GetStandardsAsync(RentalStandardQueryRequest request);
    Task<RentalStandardDto?> GetStandardByIdAsync(long id);
    Task<RentalStandardDto> CreateStandardAsync(CreateRentalStandardRequest request);
    Task<RentalStandardDto?> UpdateStandardAsync(long id, UpdateRentalStandardRequest request);
    Task<bool> UpdateStatusAsync(long id, int status);
    Task<List<RentalStandardListItemDto>> GetAllEnabledStandardsAsync();
}
