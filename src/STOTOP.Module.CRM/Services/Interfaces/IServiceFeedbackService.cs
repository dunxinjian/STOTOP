using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;

namespace STOTOP.Module.CRM.Services.Interfaces;

public interface IServiceFeedbackService
{
    Task<PagedResult<ServiceFeedbackListItemDto>> GetFeedbacksAsync(ServiceFeedbackQueryRequest request);
    Task<ServiceFeedbackDto?> GetFeedbackByIdAsync(long id);
    Task<ServiceFeedbackDto> CreateFeedbackAsync(CreateServiceFeedbackRequest request);
    Task<ServiceFeedbackDto?> UpdateFeedbackAsync(long id, UpdateServiceFeedbackRequest request);
    Task<bool> DeleteFeedbackAsync(long id);
    Task<bool> HandleFeedbackAsync(long id, HandleFeedbackRequest request);
}
