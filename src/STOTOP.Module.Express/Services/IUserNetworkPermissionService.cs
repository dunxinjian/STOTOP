using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IUserNetworkPermissionService
{
    Task<PagedResult<UserNetworkPermissionDto>> GetListAsync(UserNetworkPermissionQueryRequest request);
    Task<UserNetworkPermissionDto?> GetByIdAsync(long id);
    Task<UserNetworkPermissionDto> CreateAsync(CreateUserNetworkPermissionRequest request);
    Task<bool> DeleteAsync(long id);
}
