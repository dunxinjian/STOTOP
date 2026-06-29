using STOTOP.Core.Models;
using STOTOP.Module.Insurance.Dtos;

namespace STOTOP.Module.Insurance.Services.Interfaces;

public interface IInsClaimService
{
    Task<PagedResult<InsClaimListItemDto>> GetListAsync(InsClaimQueryRequest request);
    Task<InsClaimDto?> GetByIdAsync(long id);
    Task<InsClaimDto> CreateAsync(CreateInsClaimRequest request);
    Task<InsClaimDto?> UpdateAsync(long id, UpdateInsClaimRequest request);
    Task<bool> CloseAsync(long id, string? closedRemark);
}
