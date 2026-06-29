using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;

namespace STOTOP.Module.CRM.Services.Interfaces;

public interface IServiceOrderService
{
    Task<PagedResult<ServiceOrderListItemDto>> GetServiceOrdersAsync(ServiceOrderQueryRequest request);
    Task<ServiceOrderDto?> GetServiceOrderByIdAsync(long id);
    Task<ServiceOrderDto> CreateServiceOrderAsync(CreateServiceOrderRequest request);
    Task<ServiceOrderDto?> UpdateServiceOrderAsync(long id, UpdateServiceOrderRequest request);
    Task<bool> DeleteServiceOrderAsync(long id);
    Task<bool> ExecuteActionAsync(long id, ServiceOrderActionRequest request);
    Task<ServiceOrderStatisticsDto> GetStatisticsAsync(long? assigneeId = null);
}
