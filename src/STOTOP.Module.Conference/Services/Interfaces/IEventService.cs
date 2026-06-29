using STOTOP.Core.Models;
using STOTOP.Module.Conference.Dtos;

namespace STOTOP.Module.Conference.Services.Interfaces;

public interface IEventService
{
    Task<PagedResult<EventListItemDto>> GetEventsAsync(EventQueryRequest request);
    Task<EventDto?> GetEventByIdAsync(int id);
    Task<EventDto> CreateEventAsync(CreateEventRequest request);
    Task<EventDto?> UpdateEventAsync(int id, UpdateEventRequest request);
    Task<bool> DeleteEventAsync(int id);
    Task<DashboardDto> GetDashboardAsync(int eventId);
    Task<List<AlertItemDto>> GetAlertsAsync(int eventId);
}
