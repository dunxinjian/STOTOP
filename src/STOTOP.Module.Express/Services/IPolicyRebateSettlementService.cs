using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IPolicyRebateSettlementService
{
    Task<PolicyRebateSettlementDetailDto> ExecuteSettlementAsync(long policyRebateId, DateTime periodStart, DateTime periodEnd);
    Task<PagedResult<PolicyRebateSettlementListItemDto>> GetPagedListAsync(SettlementQueryRequest request);
    Task<PolicyRebateSettlementDetailDto?> GetDetailAsync(long id);
    Task<bool> ConfirmAsync(long id, string confirmedBy);
    Task<bool> WriteOffAsync(long id);
}
