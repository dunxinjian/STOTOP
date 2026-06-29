using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IAccountSetAuthorizationService
{
    /// <summary>
    /// 获取用户在指定组织中被授权的账套ID列表
    /// </summary>
    Task<List<long>> GetUserAccountSetIdsAsync(long userId, long orgId);

    /// <summary>
    /// 获取用户对特定账套的权限编码列表
    /// </summary>
    Task<List<string>> GetUserPermissionsAsync(long userId, long accountSetId);

    /// <summary>
    /// 检查用户是否拥有特定账套的特定权限
    /// </summary>
    Task<bool> HasPermissionAsync(long userId, long accountSetId, string permissionCode);

    /// <summary>
    /// 授权用户访问账套（指定角色）
    /// </summary>
    Task<long> GrantAsync(long userId, long accountSetId, long accountSetRoleId, long orgId, long grantedBy);

    /// <summary>
    /// 修改用户的账套角色
    /// </summary>
    Task<bool> UpdateRoleAsync(long authorizationId, long newRoleId);

    /// <summary>
    /// 撤销用户对账套的授权
    /// </summary>
    Task<bool> RevokeAsync(long authorizationId);

    /// <summary>
    /// 获取账套的已授权用户列表
    /// </summary>
    Task<List<AccountSetAuthorizationDto>> GetAccountSetUsersAsync(long accountSetId);

    /// <summary>
    /// 获取所有可用的账套角色
    /// </summary>
    Task<List<AccountSetRoleDto>> GetRolesAsync();
}
