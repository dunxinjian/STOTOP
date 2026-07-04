using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IOrgContextService
{
    Task<List<UserOrganizationDto>> GetUserOrganizationsAsync(long userId);
    Task<List<TenantMembershipDto>> GetMyTenantsAsync(long userId);
    Task<SwitchOrganizationResponse> SwitchOrganizationAsync(long userId, long orgId);

    /// <summary>阶段4C·R9：用户是否为指定租户的【已接受】成员（SYS租户成员 FInviteStatus=2 且 FStatus=1）。
    /// 供 OrgContextMiddleware 校验 X-Tenant-Context 头（防用户伪造他租户 id）。严格成员校验，admin 不旁路（跨租户走 /api/platform）。</summary>
    Task<bool> ValidateTenantMembershipAsync(long userId, long tenantId);

    /// <summary>阶段4C·R6：切换租户。校验已接受成员后，返回该租户内用户可切换组织；有主/唯一组织则附带其重算上下文。</summary>
    Task<SwitchTenantResponse> SwitchTenantAsync(long userId, long tenantId);

    Task<UserOrganizationDto?> GetCurrentContextAsync(long userId, long orgId);
    Task AddUserToOrganizationAsync(AddUserToOrganizationRequest request);
    Task UpdateUserOrganizationAsync(long id, UpdateUserOrganizationRequest request);
    Task RemoveUserFromOrganizationAsync(long id);
    Task<List<string>> GetOrgScopedRolesAsync(long userId, long orgId);
    /// <summary>M3：按当前 SYS用户组织 状态调和某用户的 SYS租户成员 + SYS任职（best-effort，供 DingTalk 批量同步后调用）。</summary>
    Task ReconcileUserMembershipBestEffortAsync(long userId);
}
