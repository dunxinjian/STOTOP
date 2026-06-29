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
public class TransportController : ControllerBase
{
    private readonly ITransportService _transportService;

    public TransportController(ITransportService transportService)
    {
        _transportService = transportService;
    }

    // === Vehicle CRUD ===

    [HttpGet("events/{eventId}/vehicles")]
    [RequirePermission(ConferencePermissions.TransportView)]
    public async Task<ApiResult<List<VehicleDto>>> GetVehicles(int eventId)
    {
        var result = await _transportService.GetVehiclesAsync(eventId);
        return ApiResult<List<VehicleDto>>.Success(result);
    }

    [HttpPost("events/{eventId}/vehicles")]
    [RequirePermission(ConferencePermissions.TransportCreate)]
    public async Task<ApiResult<VehicleDto>> CreateVehicle(int eventId, [FromBody] CreateVehicleRequest request)
    {
        try
        {
            var result = await _transportService.CreateVehicleAsync(eventId, request);
            return ApiResult<VehicleDto>.Success(result, "添加车辆成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VehicleDto>.Fail(ex.Message);
        }
    }

    [HttpPut("vehicles/{id}")]
    [RequirePermission(ConferencePermissions.TransportEdit)]
    public async Task<ApiResult<VehicleDto>> UpdateVehicle(int id, [FromBody] UpdateVehicleRequest request)
    {
        try
        {
            var result = await _transportService.UpdateVehicleAsync(id, request);
            if (result == null)
                return ApiResult<VehicleDto>.Fail("车辆不存在");
            return ApiResult<VehicleDto>.Success(result, "更新车辆成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VehicleDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("vehicles/{id}")]
    [RequirePermission(ConferencePermissions.TransportDelete)]
    public async Task<ApiResult> DeleteVehicle(int id)
    {
        var result = await _transportService.DeleteVehicleAsync(id);
        if (!result)
            return ApiResult.Fail("车辆不存在");
        return ApiResult.Ok("删除车辆成功");
    }

    // === PickupTask CRUD ===

    [HttpGet("events/{eventId}/pickups")]
    [RequirePermission(ConferencePermissions.TransportView)]
    public async Task<ApiResult<List<PickupTaskListItemDto>>> GetPickupTasks(int eventId)
    {
        var result = await _transportService.GetPickupTasksAsync(eventId);
        return ApiResult<List<PickupTaskListItemDto>>.Success(result);
    }

    [HttpGet("pickups/{id}")]
    [RequirePermission(ConferencePermissions.TransportView)]
    public async Task<ApiResult<PickupTaskDetailDto>> GetPickupTask(long id)
    {
        var result = await _transportService.GetPickupTaskDetailAsync(id);
        if (result == null)
            return ApiResult<PickupTaskDetailDto>.Fail("接送任务不存在");
        return ApiResult<PickupTaskDetailDto>.Success(result);
    }

    [HttpPost("events/{eventId}/pickups")]
    [RequirePermission(ConferencePermissions.TransportCreate)]
    public async Task<ApiResult<PickupTaskDto>> CreatePickupTask(int eventId, [FromBody] CreatePickupTaskRequest request)
    {
        try
        {
            var result = await _transportService.CreatePickupTaskAsync(eventId, request);
            return ApiResult<PickupTaskDto>.Success(result, "创建接送任务成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<PickupTaskDto>.Fail(ex.Message);
        }
    }

    [HttpPut("pickups/{id}")]
    [RequirePermission(ConferencePermissions.TransportEdit)]
    public async Task<ApiResult<PickupTaskDto>> UpdatePickupTask(int id, [FromBody] UpdatePickupTaskRequest request)
    {
        try
        {
            var result = await _transportService.UpdatePickupTaskAsync(id, request);
            if (result == null)
                return ApiResult<PickupTaskDto>.Fail("接送任务不存在");
            return ApiResult<PickupTaskDto>.Success(result, "更新接送任务成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<PickupTaskDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("pickups/{id}")]
    [RequirePermission(ConferencePermissions.TransportDelete)]
    public async Task<ApiResult> DeletePickupTask(int id)
    {
        var result = await _transportService.DeletePickupTaskAsync(id);
        if (!result)
            return ApiResult.Fail("接送任务不存在");
        return ApiResult.Ok("删除接送任务成功");
    }

    [HttpPut("pickups/{id}/passengers")]
    [RequirePermission(ConferencePermissions.TransportEdit)]
    public async Task<ApiResult> SetPassengers(int id, [FromBody] PickupPassengerRequest request)
    {
        var result = await _transportService.SetPassengersAsync(id, request);
        if (!result)
            return ApiResult.Fail("接送任务不存在");
        return ApiResult.Ok("设置乘客成功");
    }

    // === Smart Algorithms ===

    [HttpPost("events/{eventId}/pickups/auto-generate")]
    [RequirePermission(ConferencePermissions.TransportEdit)]
    public async Task<ApiResult<AutoGeneratePreviewDto>> AutoGeneratePickups(int eventId)
    {
        var result = await _transportService.AutoGeneratePickupsAsync(eventId);
        return ApiResult<AutoGeneratePreviewDto>.Success(result);
    }

    [HttpPost("events/{eventId}/pickups/auto-generate/commit")]
    [RequirePermission(ConferencePermissions.TransportEdit)]
    public async Task<ApiResult<List<PickupTaskDto>>> CommitAutoGenerate(int eventId, [FromBody] CommitAutoGenerateRequest request)
    {
        try
        {
            var result = await _transportService.CommitAutoGenerateAsync(eventId, request.Tasks);
            return ApiResult<List<PickupTaskDto>>.Success(result, "自动生成接送任务成功");
        }
        catch (Exception ex)
        {
            return ApiResult<List<PickupTaskDto>>.Fail($"提交失败: {ex.Message}");
        }
    }

    [HttpPost("events/{eventId}/pickups/optimize")]
    [RequirePermission(ConferencePermissions.TransportEdit)]
    public async Task<ApiResult<OptimizePreviewDto>> OptimizePickups(int eventId)
    {
        var result = await _transportService.OptimizePickupsAsync(eventId);
        return ApiResult<OptimizePreviewDto>.Success(result);
    }

    // === Export ===

    [HttpGet("events/{eventId}/pickups/export-pdf")]
    [RequirePermission(ConferencePermissions.TransportView)]
    public async Task<IActionResult> ExportPickupsPdf(int eventId)
    {
        var bytes = await _transportService.ExportPickupsPdfAsync(eventId);
        return File(bytes, "application/pdf", "接送安排.pdf");
    }

    [HttpGet("events/{eventId}/pickups/export-image")]
    [RequirePermission(ConferencePermissions.TransportView)]
    public async Task<IActionResult> ExportPickupsImage(int eventId)
    {
        var bytes = await _transportService.ExportPickupsImageAsync(eventId);
        return File(bytes, "image/png", "接送安排.png");
    }
}
