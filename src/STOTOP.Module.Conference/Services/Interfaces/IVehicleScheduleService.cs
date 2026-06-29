using STOTOP.Module.Conference.Dtos;

namespace STOTOP.Module.Conference.Services.Interfaces;

public interface IVehicleScheduleService
{
    Task<List<VehicleScheduleDto>> GetVehicleSchedulesAsync(int eventId);
    Task<List<VehicleScheduleDto>> GetByVehicleAsync(int eventId, int vehicleId);
    Task<List<VehicleScheduleDto>> GetByDateAsync(int eventId, DateTime date);
    Task<VehicleScheduleGeneratePreviewDto> GenerateSchedulesAsync(int eventId);
    Task<VehicleScheduleDto> AddVehicleTaskAsync(int eventId, AddVehicleTaskRequest request);
    Task<VehicleScheduleDto?> UpdateScheduleAsync(int id, AddVehicleTaskRequest request);
    Task<bool> DeleteScheduleAsync(int id);
    Task<byte[]> ExportPdfAsync(int eventId);
    Task<List<DriverCardDto>> GetDriverCardsAsync(int eventId);
    Task<List<DriverNotificationDto>> GetDriverNotificationsAsync(long eventId, DateTime date);
}
