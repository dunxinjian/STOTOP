using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;

namespace STOTOP.Module.CRM.Services.Interfaces;

public interface ICrmOrgService
{
    Task<PagedResult<CrmRoleMappingListItemDto>> GetRoleMappingsAsync(RoleMappingQueryRequest request);
    Task<CrmRoleMappingDto?> GetRoleMappingByIdAsync(long id);
    Task<CrmRoleMappingDto> CreateRoleMappingAsync(CreateRoleMappingRequest request);
    Task<CrmRoleMappingDto?> UpdateRoleMappingAsync(long id, UpdateRoleMappingRequest request);
    Task<bool> DeleteRoleMappingAsync(long id);
    Task<List<CrmRoleMappingListItemDto>> GetBdListAsync(long orgId);
    Task<List<CrmRoleMappingListItemDto>> GetMaintenanceListAsync(long orgId);
}
