using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IRoleService
{
    Task<ApiResult<List<RoleDto>>> GetAllAsync();
    Task<ApiResult<RoleDto>> GetByIdAsync(long id);
    Task<ApiResult<RoleDto>> CreateAsync(CreateRoleRequest request);
    Task<ApiResult<RoleDto>> UpdateAsync(long id, UpdateRoleRequest request);
    Task<ApiResult<bool>> DeleteAsync(long id);
    Task<ApiResult<bool>> AssignPermissionsAsync(long roleId, List<long> permissionIds);
}
