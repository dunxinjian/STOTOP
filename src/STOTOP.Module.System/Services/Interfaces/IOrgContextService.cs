using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IOrgContextService
{
    Task<List<UserOrganizationDto>> GetUserOrganizationsAsync(long userId);
    Task<SwitchOrganizationResponse> SwitchOrganizationAsync(long userId, long orgId);
    Task<UserOrganizationDto?> GetCurrentContextAsync(long userId, long orgId);
    Task AddUserToOrganizationAsync(AddUserToOrganizationRequest request);
    Task UpdateUserOrganizationAsync(long id, UpdateUserOrganizationRequest request);
    Task RemoveUserFromOrganizationAsync(long id);
    Task<List<string>> GetOrgScopedRolesAsync(long userId, long orgId);
}
