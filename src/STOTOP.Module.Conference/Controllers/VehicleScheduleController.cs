using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Conference.Controllers;

[Authorize]
[ApiController]
[Route("api/conference")]
public class VehicleScheduleController : ControllerBase
{
    private readonly IVehicleScheduleService _vehicleScheduleService;

    public VehicleScheduleController(IVehicleScheduleService vehicleScheduleService)
    {
        _vehicleScheduleService = vehicleScheduleService;
    }

    [HttpGet("events/{eventId}/vehicle-schedules")]
    [RequirePermission(ConferencePermissions.VehicleScheduleView)]
    public async Task<ApiResult<List<VehicleScheduleDto>>> GetVehicleSchedules(int eventId)
    {
        var result = await _vehicleScheduleService.GetVehicleSchedulesAsync(eventId);
        return ApiResult<List<VehicleScheduleDto>>.Success(result);
    }

    [HttpGet("events/{eventId}/vehicle-schedules/by-vehicle/{vehicleId}")]
    [RequirePermission(ConferencePermissions.VehicleScheduleView)]
    public async Task<ApiResult<List<VehicleScheduleDto>>> GetByVehicle(int eventId, int vehicleId)
    {
        var result = await _vehicleScheduleService.GetByVehicleAsync(eventId, vehicleId);
        return ApiResult<List<VehicleScheduleDto>>.Success(result);
    }

    [HttpGet("events/{eventId}/vehicle-schedules/by-date/{date}")]
    [RequirePermission(ConferencePermissions.VehicleScheduleView)]
    public async Task<ApiResult<List<VehicleScheduleDto>>> GetByDate(int eventId, DateTime date)
    {
        var result = await _vehicleScheduleService.GetByDateAsync(eventId, date);
        return ApiResult<List<VehicleScheduleDto>>.Success(result);
    }

    [HttpPost("events/{eventId}/vehicle-schedules/generate")]
    [RequirePermission(ConferencePermissions.VehicleScheduleEdit)]
    public async Task<ApiResult<VehicleScheduleGeneratePreviewDto>> GenerateSchedules(int eventId)
    {
        var result = await _vehicleScheduleService.GenerateSchedulesAsync(eventId);
        return ApiResult<VehicleScheduleGeneratePreviewDto>.Success(result);
    }

    [HttpPost("events/{eventId}/vehicle-schedules/add-task")]
    [RequirePermission(ConferencePermissions.VehicleScheduleEdit)]
    public async Task<ApiResult<VehicleScheduleDto>> AddVehicleTask(int eventId, [FromBody] AddVehicleTaskRequest request)
    {
        try
        {
            var result = await _vehicleScheduleService.AddVehicleTaskAsync(eventId, request);
            return ApiResult<VehicleScheduleDto>.Success(result, "添加车辆任务成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VehicleScheduleDto>.Fail(ex.Message);
        }
    }

    [HttpPut("vehicle-schedules/{id}")]
    [RequirePermission(ConferencePermissions.VehicleScheduleEdit)]
    public async Task<ApiResult<VehicleScheduleDto>> UpdateSchedule(int id, [FromBody] AddVehicleTaskRequest request)
    {
        try
        {
            var result = await _vehicleScheduleService.UpdateScheduleAsync(id, request);
            if (result == null)
                return ApiResult<VehicleScheduleDto>.Fail("车辆日程不存在");
            return ApiResult<VehicleScheduleDto>.Success(result, "更新车辆日程成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VehicleScheduleDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("vehicle-schedules/{id}")]
    [RequirePermission(ConferencePermissions.VehicleScheduleEdit)]
    public async Task<ApiResult> DeleteSchedule(int id)
    {
        var result = await _vehicleScheduleService.DeleteScheduleAsync(id);
        if (!result)
            return ApiResult.Fail("车辆日程不存在");
        return ApiResult.Ok("删除车辆日程成功");
    }

    [HttpGet("events/{eventId}/vehicle-schedules/export-pdf")]
    [RequirePermission(ConferencePermissions.VehicleScheduleView)]
    public async Task<IActionResult> ExportPdf(int eventId)
    {
        var bytes = await _vehicleScheduleService.ExportPdfAsync(eventId);
        return File(bytes, "application/pdf", "车辆日程.pdf");
    }

    [HttpGet("events/{eventId}/vehicle-schedules/driver-cards")]
    [RequirePermission(ConferencePermissions.VehicleScheduleView)]
    public async Task<ApiResult<List<DriverCardDto>>> GetDriverCards(int eventId)
    {
        var result = await _vehicleScheduleService.GetDriverCardsAsync(eventId);
        return ApiResult<List<DriverCardDto>>.Success(result);
    }

    [HttpGet("events/{eventId}/vehicle-schedules/driver-notifications")]
    [RequirePermission(ConferencePermissions.VehicleScheduleView)]
    public async Task<ApiResult<List<DriverNotificationDto>>> GetDriverNotifications(long eventId, [FromQuery] DateTime date)
    {
        var result = await _vehicleScheduleService.GetDriverNotificationsAsync(eventId, date);
        return ApiResult<List<DriverNotificationDto>>.Success(result);
    }
}
