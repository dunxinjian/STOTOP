using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;

namespace STOTOP.Module.Contract.Services.Interfaces;

public interface IContractReminderService
{
    Task<PagedResult<ContractReminderDto>> GetRemindersAsync(ContractReminderQueryRequest request);
    Task<ContractReminderDto?> GetReminderByIdAsync(long id);
    Task<ContractReminderDto> CreateReminderAsync(CreateContractReminderRequest request);
    Task<ContractReminderDto?> UpdateReminderAsync(long id, UpdateContractReminderRequest request);
    Task<bool> DeleteReminderAsync(long id);
    Task<List<ContractReminderDto>> GetPendingRemindersAsync(long recipientId);
    Task<bool> MarkAsHandledAsync(long id);
}
