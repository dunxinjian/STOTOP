using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IOrgContextService
{
    Task<List<UserOrganizationDto>> GetUserOrganizationsAsync(long userId);
    Task<List<TenantMembershipDto>> GetMyTenantsAsync(long userId);
    Task<SwitchOrganizationResponse> SwitchOrganizationAsync(long userId, long orgId);
    Task<UserOrganizationDto?> GetCurrentContextAsync(long userId, long orgId);
    Task AddUserToOrganizationAsync(AddUserToOrganizationRequest request);
    Task UpdateUserOrganizationAsync(long id, UpdateUserOrganizationRequest request);
    Task RemoveUserFromOrganizationAsync(long id);
    Task<List<string>> GetOrgScopedRolesAsync(long userId, long orgId);
    /// <summary>M3：按当前 SYS用户组织 状态调和某用户的 SYS租户成员 + SYS任职（best-effort，供 DingTalk 批量同步后调用）。</summary>
    Task ReconcileUserMembershipBestEffortAsync(long userId);
}
