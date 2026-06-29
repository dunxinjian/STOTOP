using STOTOP.Module.Vehicle.Dtos;

namespace STOTOP.Module.Vehicle.Services.Interfaces;

public interface IGpsService
{
    Task<VehicleLocationDto?> GetCurrentLocationAsync(long vehicleId);
    Task<List<VehicleTrackPointDto>> GetTrackAsync(long vehicleId, DateTime startTime, DateTime endTime);
}
