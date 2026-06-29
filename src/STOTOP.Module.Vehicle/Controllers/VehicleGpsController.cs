using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Vehicle.Dtos;
using STOTOP.Module.Vehicle.Services.Interfaces;

namespace STOTOP.Module.Vehicle.Controllers;

/// <summary>
/// 车辆GPS管理控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/vehicle/gps")]
public class VehicleGpsController : ControllerBase
{
    private readonly IGpsService _gpsService;

    public VehicleGpsController(IGpsService gpsService)
    {
        _gpsService = gpsService;
    }

    /// <summary>
    /// 获取车辆当前位置
    /// </summary>
    [HttpGet("{vehicleId}/location")]
    [RequirePermission(VehiclePermissions.GpsView)]
    public async Task<ApiResult<VehicleLocationDto>> GetLocation(long vehicleId)
    {
        var result = await _gpsService.GetCurrentLocationAsync(vehicleId);
        if (result == null)
        {
            return ApiResult<VehicleLocationDto>.Fail("车辆位置信息不存在");
        }
        return ApiResult<VehicleLocationDto>.Success(result);
    }

    /// <summary>
    /// 获取车辆轨迹
    /// </summary>
    [HttpGet("{vehicleId}/track")]
    [RequirePermission(VehiclePermissions.GpsView)]
    public async Task<ApiResult<List<VehicleTrackPointDto>>> GetTrack(long vehicleId, [FromQuery] VehicleTrackQueryRequest request)
    {
        var result = await _gpsService.GetTrackAsync(vehicleId, request.StartTime, request.EndTime);
        return ApiResult<List<VehicleTrackPointDto>>.Success(result);
    }
}
