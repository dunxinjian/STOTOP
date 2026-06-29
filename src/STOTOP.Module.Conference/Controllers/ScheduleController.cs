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
public class ScheduleController : ControllerBase
{
    private readonly IScheduleService _scheduleService;

    public ScheduleController(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    [HttpGet("events/{eventId}/schedules")]
    [RequirePermission(ConferencePermissions.ScheduleView)]
    public async Task<ApiResult<List<ScheduleDto>>> GetList(int eventId)
    {
        var result = await _scheduleService.GetSchedulesAsync(eventId);
        return ApiResult<List<ScheduleDto>>.Success(result);
    }

    [HttpPost("events/{eventId}/schedules")]
    [RequirePermission(ConferencePermissions.ScheduleCreate)]
    public async Task<ApiResult<ScheduleDto>> Create(int eventId, [FromBody] CreateScheduleRequest request)
    {
        try
        {
            var result = await _scheduleService.CreateScheduleAsync(eventId, request);
            return ApiResult<ScheduleDto>.Success(result, "创建日程成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ScheduleDto>.Fail(ex.Message);
        }
    }

    [HttpPut("schedules/{id}")]
    [RequirePermission(ConferencePermissions.ScheduleEdit)]
    public async Task<ApiResult<ScheduleDto>> Update(int id, [FromBody] UpdateScheduleRequest request)
    {
        try
        {
            var result = await _scheduleService.UpdateScheduleAsync(id, request);
            if (result == null)
                return ApiResult<ScheduleDto>.Fail("日程不存在");
            return ApiResult<ScheduleDto>.Success(result, "更新日程成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ScheduleDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("schedules/{id}")]
    [RequirePermission(ConferencePermissions.ScheduleDelete)]
    public async Task<ApiResult> Delete(int id)
    {
        var result = await _scheduleService.DeleteScheduleAsync(id);
        if (!result)
            return ApiResult.Fail("日程不存在");
        return ApiResult.Ok("删除日程成功");
    }

    [HttpPut("schedules/{id}/attendees")]
    [RequirePermission(ConferencePermissions.ScheduleEdit)]
    public async Task<ApiResult> SetAttendees(int id, [FromBody] ScheduleAttendeeRequest request)
    {
        var result = await _scheduleService.SetScheduleAttendeesAsync(id, request);
        if (!result)
            return ApiResult.Fail("日程不存在");
        return ApiResult.Ok("设置参会人员成功");
    }

    [HttpPut("schedules/{id}/items")]
    [RequirePermission(ConferencePermissions.ScheduleEdit)]
    public async Task<ApiResult> SetItems(int id, [FromBody] ScheduleItemRequest request)
    {
        var result = await _scheduleService.SetScheduleItemsAsync(id, request);
        if (!result)
            return ApiResult.Fail("日程不存在");
        return ApiResult.Ok("设置议程项成功");
    }
}
