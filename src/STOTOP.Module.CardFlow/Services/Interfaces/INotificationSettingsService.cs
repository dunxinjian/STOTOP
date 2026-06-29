using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface INotificationSettingsService
{
    Task<NotificationSettingsDto> GetAsync(long orgId);
    Task SaveAsync(SaveNotificationSettingsRequest request, long orgId, long operatorId);
    Task<TestNotificationResult> TestAsync(TestNotificationRequest request);
}
