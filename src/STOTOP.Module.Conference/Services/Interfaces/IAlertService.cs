using STOTOP.Module.Conference.Dtos;

namespace STOTOP.Module.Conference.Services.Interfaces;

public interface IAlertService
{
    Task<List<AlertItemDto>> ScanAllAlertsAsync(int eventId);
}
