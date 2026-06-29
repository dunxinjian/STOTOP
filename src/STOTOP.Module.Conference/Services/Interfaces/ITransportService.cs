using STOTOP.Module.Conference.Dtos;

namespace STOTOP.Module.Conference.Services.Interfaces;

public interface ITransportService
{
    // Vehicle CRUD
    Task<List<VehicleDto>> GetVehiclesAsync(int eventId);
    Task<VehicleDto> CreateVehicleAsync(int eventId, CreateVehicleRequest request);
    Task<VehicleDto?> UpdateVehicleAsync(int id, UpdateVehicleRequest request);
    Task<bool> DeleteVehicleAsync(int id);

    // PickupTask CRUD
    Task<List<PickupTaskListItemDto>> GetPickupTasksAsync(int eventId);
    Task<PickupTaskDetailDto?> GetPickupTaskDetailAsync(long taskId);
    Task<PickupTaskDto> CreatePickupTaskAsync(int eventId, CreatePickupTaskRequest request);
    Task<PickupTaskDto?> UpdatePickupTaskAsync(int id, UpdatePickupTaskRequest request);
    Task<bool> DeletePickupTaskAsync(int id);
    Task<bool> SetPassengersAsync(int id, PickupPassengerRequest request);

    // Smart algorithms
    Task<AutoGeneratePreviewDto> AutoGeneratePickupsAsync(int eventId);
    Task<List<PickupTaskDto>> CommitAutoGenerateAsync(int eventId, List<PickupTaskPreviewItem> tasksToCommit);
    Task<OptimizePreviewDto> OptimizePickupsAsync(int eventId);

    // Export (PDF/Image)
    Task<byte[]> ExportPickupsPdfAsync(int eventId);
    Task<byte[]> ExportPickupsImageAsync(int eventId);
}
