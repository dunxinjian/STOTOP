using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IPermissionService
{
    Task<ApiResult<List<PermissionDto>>> GetTreeAsync();
    Task<ApiResult<List<MenuTreeResponse>>> GetMenuTreeAsync(long userId);
    Task<ApiResult<PermissionDto>> CreateAsync(CreatePermissionRequest request);
    Task<ApiResult<PermissionDto>> UpdateAsync(long id, UpdatePermissionRequest request);
    Task<ApiResult<bool>> DeleteAsync(long id);
    Task<ApiResult<List<string>>> GetCurrentPermissionsAsync(long userId);
}
