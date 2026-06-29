using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IPolicyRebateService
{
    Task<PagedResult<PolicyRebateListItemDto>> GetPagedListAsync(PolicyRebateQueryRequest request);
    Task<PolicyRebateDetailDto?> GetDetailAsync(long id);
    Task<PolicyRebateDetailDto> CreateAsync(CreatePolicyRebateRequest request);
    Task<PolicyRebateDetailDto?> UpdateAsync(long id, UpdatePolicyRebateRequest request);
    Task<bool> DeleteAsync(long id);
    Task<bool> EnableAsync(long id);
    Task<bool> DisableAsync(long id);
}
