using STOTOP.Core.Models;
using STOTOP.Module.Vehicle.Dtos;

namespace STOTOP.Module.Vehicle.Services.Interfaces;

public interface IMaintenanceService
{
    Task<PagedResult<VehicleMaintenanceListItemDto>> GetMaintenancesAsync(MaintenanceQueryRequest request);
    Task<VehicleMaintenanceDto?> GetMaintenanceByIdAsync(long id);
    Task<VehicleMaintenanceDto> CreateMaintenanceAsync(CreateMaintenanceRequest request);
    Task<VehicleMaintenanceDto?> CompleteMaintenanceAsync(long id, CompleteMaintenanceRequest request);
}
