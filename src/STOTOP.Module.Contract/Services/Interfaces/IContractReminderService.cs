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
    /// <summary>待处理合同提醒列表；take&gt;0 时按最近提醒日期封顶取前 N 条（WorkHub 瘦身），take==0 取全量（保持原有调用方语义）。</summary>
    Task<List<ContractReminderDto>> GetPendingRemindersAsync(long recipientId, int take = 0);
    /// <summary>待处理合同提醒数量（WorkHub 角标口径：F接收人ID==userId 且 未处理）</summary>
    Task<int> GetPendingCountAsync(long userId);
    Task<bool> MarkAsHandledAsync(long id);
}
