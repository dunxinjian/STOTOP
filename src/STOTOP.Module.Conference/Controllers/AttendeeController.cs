using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Conference.Controllers;

[Authorize]
[ApiController]
[Route("api/conference")]
public class AttendeeController : ControllerBase
{
    private readonly IAttendeeService _attendeeService;

    public AttendeeController(IAttendeeService attendeeService)
    {
        _attendeeService = attendeeService;
    }

    [HttpGet("events/{eventId}/attendees")]
    [RequirePermission(ConferencePermissions.AttendeeView)]
    public async Task<ApiResult<PagedResult<AttendeeListItemDto>>> GetList(int eventId, [FromQuery] AttendeeQueryRequest request)
    {
        var result = await _attendeeService.GetAttendeesAsync(eventId, request);
        return ApiResult<PagedResult<AttendeeListItemDto>>.Success(result);
    }

    [HttpGet("attendees/{id}")]
    [RequirePermission(ConferencePermissions.AttendeeView)]
    public async Task<ApiResult<AttendeeDto>> GetById(int id)
    {
        var result = await _attendeeService.GetAttendeeByIdAsync(id);
        if (result == null)
            return ApiResult<AttendeeDto>.Fail("参会人员不存在");
        return ApiResult<AttendeeDto>.Success(result);
    }

    [HttpPost("events/{eventId}/attendees")]
    [RequirePermission(ConferencePermissions.AttendeeCreate)]
    public async Task<ApiResult<AttendeeDto>> Create(int eventId, [FromBody] CreateAttendeeRequest request)
    {
        try
        {
            var result = await _attendeeService.CreateAttendeeAsync(eventId, request);
            return ApiResult<AttendeeDto>.Success(result, "添加参会人员成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AttendeeDto>.Fail(ex.Message);
        }
    }

    [HttpPut("attendees/{id}")]
    [RequirePermission(ConferencePermissions.AttendeeEdit)]
    public async Task<ApiResult<AttendeeDto>> Update(int id, [FromBody] UpdateAttendeeRequest request)
    {
        try
        {
            var result = await _attendeeService.UpdateAttendeeAsync(id, request);
            if (result == null)
                return ApiResult<AttendeeDto>.Fail("参会人员不存在");
            return ApiResult<AttendeeDto>.Success(result, "更新参会人员成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AttendeeDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("attendees/{id}")]
    [RequirePermission(ConferencePermissions.AttendeeDelete)]
    public async Task<ApiResult> Delete(int id)
    {
        var result = await _attendeeService.DeleteAttendeeAsync(id);
        if (!result)
            return ApiResult.Fail("参会人员不存在");
        return ApiResult.Ok("删除参会人员成功");
    }

    [HttpPost("attendees/{id}/impact-analysis")]
    [RequirePermission(ConferencePermissions.AttendeeView)]
    public async Task<ApiResult<AttendeeImpactAnalysisDto>> GetImpactAnalysis(int id)
    {
        var result = await _attendeeService.GetImpactAnalysisAsync(id);
        return ApiResult<AttendeeImpactAnalysisDto>.Success(result);
    }

    [HttpPost("attendees/{id}/apply-changes")]
    [RequirePermission(ConferencePermissions.AttendeeEdit)]
    public async Task<ApiResult> ApplyChanges(int id)
    {
        var result = await _attendeeService.ApplyChangesAsync(id);
        if (!result)
            return ApiResult.Fail("操作失败");
        return ApiResult.Ok("变更应用成功");
    }

    [HttpPost("events/{eventId}/attendees/import")]
    [RequirePermission(ConferencePermissions.AttendeeImport)]
    public async Task<ApiResult<List<AttendeeDto>>> Import(int eventId, IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var result = await _attendeeService.ImportAttendeesAsync(eventId, stream);
        return ApiResult<List<AttendeeDto>>.Success(result, "导入成功");
    }

    [HttpGet("events/{eventId}/attendees/export")]
    [RequirePermission(ConferencePermissions.AttendeeExport)]
    public async Task<IActionResult> Export(int eventId)
    {
        var bytes = await _attendeeService.ExportAttendeesAsync(eventId);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "参会人员.xlsx");
    }

    [HttpGet("attendees/import-template")]
    [RequirePermission(ConferencePermissions.AttendeeImport)]
    public async Task<IActionResult> DownloadImportTemplate()
    {
        var bytes = await _attendeeService.GenerateImportTemplateAsync();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "参会人员导入模板.xlsx");
    }

    [HttpPost("events/{eventId}/attendees/{primaryGuestId}/companions")]
    [RequirePermission(ConferencePermissions.AttendeeCreate)]
    public async Task<ApiResult<AttendeeDto>> CreateCompanion(int eventId, long primaryGuestId, [FromBody] CreateAttendeeRequest request)
    {
        try
        {
            request.PrimaryGuestId = primaryGuestId;
            var result = await _attendeeService.CreateAttendeeAsync(eventId, request);
            return ApiResult<AttendeeDto>.Success(result, "添加随行人员成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AttendeeDto>.Fail(ex.Message);
        }
    }

    [HttpGet("attendees/{id}/companions")]
    [RequirePermission(ConferencePermissions.AttendeeView)]
    public async Task<ApiResult<List<AttendeeDto>>> GetCompanions(long id)
    {
        var result = await _attendeeService.GetCompanionsAsync(id);
        return ApiResult<List<AttendeeDto>>.Success(result);
    }

    [HttpPut("attendees/batch-status")]
    [RequirePermission(ConferencePermissions.AttendeeEdit)]
    public async Task<ApiResult<object>> BatchUpdateStatus([FromBody] BatchUpdateStatusRequest request)
    {
        await _attendeeService.BatchUpdateStatusAsync(request);
        return ApiResult<object>.Success(null!, "批量更新状态成功");
    }

    [HttpPut("attendees/{attendeeId}/room-preference")]
    [RequirePermission(ConferencePermissions.AttendeeEdit)]
    public async Task<ApiResult> UpdateRoomPreference(long attendeeId, [FromBody] UpdateRoomPreferenceRequest request)
    {
        try
        {
            await _attendeeService.UpdateRoomPreferenceAsync(attendeeId, request.PreferredRoomType);
            return ApiResult.Ok("房型偏好已更新");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
