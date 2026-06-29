using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Conference.Controllers;

[Authorize]
[ApiController]
[Route("api/conference/events")]
public class EventController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventController(IEventService eventService)
    {
        _eventService = eventService;
    }

    [HttpGet]
    [RequirePermission(ConferencePermissions.EventView)]
    public async Task<ApiResult<PagedResult<EventListItemDto>>> GetList([FromQuery] EventQueryRequest request)
    {
        var result = await _eventService.GetEventsAsync(request);
        return ApiResult<PagedResult<EventListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(ConferencePermissions.EventView)]
    public async Task<ApiResult<EventDto>> GetById(int id)
    {
        var result = await _eventService.GetEventByIdAsync(id);
        if (result == null)
            return ApiResult<EventDto>.Fail("活动不存在");
        return ApiResult<EventDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(ConferencePermissions.EventCreate)]
    public async Task<ApiResult<EventDto>> Create([FromBody] CreateEventRequest request)
    {
        try
        {
            var result = await _eventService.CreateEventAsync(request);
            return ApiResult<EventDto>.Success(result, "创建活动成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<EventDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    [RequirePermission(ConferencePermissions.EventEdit)]
    public async Task<ApiResult<EventDto>> Update(int id, [FromBody] UpdateEventRequest request)
    {
        try
        {
            var result = await _eventService.UpdateEventAsync(id, request);
            if (result == null)
                return ApiResult<EventDto>.Fail("活动不存在");
            return ApiResult<EventDto>.Success(result, "更新活动成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<EventDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    [RequirePermission(ConferencePermissions.EventDelete)]
    public async Task<ApiResult> Delete(int id)
    {
        var result = await _eventService.DeleteEventAsync(id);
        if (!result)
            return ApiResult.Fail("活动不存在");
        return ApiResult.Ok("删除活动成功");
    }

    [HttpGet("{id}/dashboard")]
    [RequirePermission(ConferencePermissions.EventView)]
    public async Task<ApiResult<DashboardDto>> GetDashboard(int id)
    {
        var result = await _eventService.GetDashboardAsync(id);
        return ApiResult<DashboardDto>.Success(result);
    }

    [HttpGet("{id}/alerts")]
    [RequirePermission(ConferencePermissions.EventView)]
    public async Task<ApiResult<List<AlertItemDto>>> GetAlerts(int id)
    {
        var result = await _eventService.GetAlertsAsync(id);
        return ApiResult<List<AlertItemDto>>.Success(result);
    }
}
