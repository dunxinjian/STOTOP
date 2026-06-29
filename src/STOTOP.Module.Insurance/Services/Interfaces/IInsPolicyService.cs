using STOTOP.Core.Models;
using STOTOP.Module.Insurance.Dtos;

namespace STOTOP.Module.Insurance.Services.Interfaces;

public interface IInsPolicyService
{
    Task<PagedResult<InsPolicyListItemDto>> GetListAsync(InsPolicyQueryRequest request);
    Task<InsPolicyDto?> GetByIdAsync(long id);
    Task<InsPolicyDto> CreateAsync(CreateInsPolicyRequest request);
    Task<InsPolicyDto?> UpdateAsync(long id, UpdateInsPolicyRequest request);
    Task<List<InsPolicyListItemDto>> GetExpiringAsync(int days = 30);
    Task<List<InsPolicyListItemDto>> GetByObjectAsync(int bizType, long objectId);
}
