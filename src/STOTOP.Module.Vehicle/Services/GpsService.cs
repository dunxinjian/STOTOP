using STOTOP.Module.Vehicle.Dtos;
using STOTOP.Module.Vehicle.Services.Interfaces;

namespace STOTOP.Module.Vehicle.Services;

public class GpsService : IGpsService
{
    public Task<VehicleLocationDto?> GetCurrentLocationAsync(long vehicleId)
    {
        // GPS 功能预留，待对接第三方 GPS 平台后实现
        return Task.FromResult<VehicleLocationDto?>(null);
    }

    public Task<List<VehicleTrackPointDto>> GetTrackAsync(long vehicleId, DateTime startTime, DateTime endTime)
    {
        // GPS 功能预留，待对接第三方 GPS 平台后实现
        return Task.FromResult(new List<VehicleTrackPointDto>());
    }
}
