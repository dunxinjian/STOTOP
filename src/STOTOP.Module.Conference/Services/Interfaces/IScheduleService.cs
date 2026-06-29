using STOTOP.Module.Conference.Dtos;

namespace STOTOP.Module.Conference.Services.Interfaces;

public interface IScheduleService
{
    Task<List<ScheduleDto>> GetSchedulesAsync(int eventId);
    Task<ScheduleDto> CreateScheduleAsync(int eventId, CreateScheduleRequest request);
    Task<ScheduleDto?> UpdateScheduleAsync(int id, UpdateScheduleRequest request);
    Task<bool> DeleteScheduleAsync(int id);
    Task<bool> SetScheduleAttendeesAsync(int id, ScheduleAttendeeRequest request);
    Task<bool> SetScheduleItemsAsync(int id, ScheduleItemRequest request);
}
