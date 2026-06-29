using STOTOP.Core.Models;
using STOTOP.Module.Insurance.Dtos;

namespace STOTOP.Module.Insurance.Services.Interfaces;

public interface ISettlementService
{
    Task<PagedResult<InsSettlementListItemDto>> GetListAsync(InsSettlementQueryRequest request);
    Task<InsSettlementDto?> GetByIdAsync(long id);
    Task<InsSettlementDto> CreateAsync(CreateInsSettlementRequest request);
    Task<InsSettlementDto?> UpdateAsync(long id, UpdateInsSettlementRequest request);
    Task SubmitAsync(long id);
    Task ReviewAsync(CreateInsApprovalRecordRequest request);
    Task<PagedResult<InsSettlementListItemDto>> GetPendingMyAsync(long approverId, int pageIndex = 1, int pageSize = 20);
    Task<List<InsApprovalRecordListItemDto>> GetApprovalHistoryAsync(long settlementId);
    Task PayAsync(long settlementId, string? paymentVoucher);
}
