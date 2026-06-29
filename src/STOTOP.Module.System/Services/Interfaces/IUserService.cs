using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IUserService
{
    Task<ApiResult<PagedResult<UserDto>>> GetPagedListAsync(UserPagedRequest request);
    Task<ApiResult<UserDto>> GetByIdAsync(long id);
    Task<ApiResult<UserDto>> CreateAsync(CreateUserRequest request);
    Task<ApiResult<UserDto>> UpdateAsync(long id, UpdateUserRequest request);
    Task<ApiResult<bool>> DeleteAsync(long id);
    Task<ApiResult<bool>> ResetPasswordAsync(long id, string newPassword);
    Task<List<UserOrganizationDto>> GetUserOrganizationsAsync(long userId);
    Task<List<PositionDto>> GetUserPositionsAsync(long userId);
}
