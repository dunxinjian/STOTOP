using STOTOP.Core.Models;
using STOTOP.Module.Vehicle.Dtos;

namespace STOTOP.Module.Vehicle.Services.Interfaces;

public interface IAssignmentService
{
    Task<PagedResult<VehicleAssignmentListItemDto>> GetAssignmentsAsync(AssignmentQueryRequest request);
    Task<VehicleAssignmentDto?> GetAssignmentByIdAsync(long id);
    Task<VehicleAssignmentDto> CreateAssignmentAsync(CreateAssignmentRequest request);
    Task<VehicleAssignmentDto?> ReturnVehicleAsync(long id, ReturnVehicleRequest request);
}
