using STOTOP.Core.Models;
using STOTOP.Module.Vehicle.Dtos;

namespace STOTOP.Module.Vehicle.Services.Interfaces;

public interface IVehicleService
{
    Task<PagedResult<VehicleListItemDto>> GetVehiclesAsync(VehicleQueryRequest request);
    Task<VehicleDto?> GetVehicleByIdAsync(long id);
    Task<VehicleDto> CreateVehicleAsync(CreateVehicleRequest request);
    Task<VehicleDto?> UpdateVehicleAsync(long id, UpdateVehicleRequest request);
    Task<bool> DeleteVehicleAsync(long id);
    Task<VehicleStatisticsDto> GetStatisticsAsync();
    Task<bool> CheckCodeExistsAsync(string code, long excludeId = 0);
}
