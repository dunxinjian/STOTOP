using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;

namespace STOTOP.Module.CRM.Services.Interfaces;

public interface ICustomerService
{
    Task<PagedResult<CustomerListItemDto>> GetCustomersAsync(CustomerQueryRequest request);
    Task<CustomerStatisticsDto> GetStatisticsAsync();
    Task<CustomerDto?> GetCustomerByCodeAsync(string code);
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request);
    Task<CustomerDto?> UpdateCustomerAsync(string code, UpdateCustomerRequest request);
    Task<bool> DeleteCustomerAsync(string code);
    Task<bool> UpdateStatusAsync(string code, int status);
    Task<bool> TransferCustomerAsync(string code, TransferCustomerRequest request);
    Task<List<CustomerListItemDto>> CheckDuplicateAsync(CustomerDuplicateCheckRequest request);
    Task<List<CustomerTimelineItemDto>> GetTimelineAsync(string code, int count = 20);
}
