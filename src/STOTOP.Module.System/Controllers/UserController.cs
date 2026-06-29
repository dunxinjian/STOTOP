using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Filters;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

[Route("api/system/users")]
[ApiController]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IOrgContextService _orgContextService;

    public UserController(IUserService userService, IOrgContextService orgContextService)
    {
        _userService = userService;
        _orgContextService = orgContextService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<UserDto>>> GetPagedList([FromQuery] UserPagedRequest request)
    {
        return await _userService.GetPagedListAsync(request);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<UserDto>> GetById(long id)
    {
        return await _userService.GetByIdAsync(id);
    }

    [RequirePermission(SystemPermissions.UserCreate)]
    [HttpPost]
    public async Task<ApiResult<UserDto>> Create([FromBody] CreateUserRequest request)
    {
        return await _userService.CreateAsync(request);
    }

    [RequirePermission(SystemPermissions.UserEdit)]
    [HttpPut("{id}")]
    public async Task<ApiResult<UserDto>> Update(long id, [FromBody] UpdateUserRequest request)
    {
        return await _userService.UpdateAsync(id, request);
    }

    [RequirePermission(SystemPermissions.UserDelete)]
    [HttpDelete("{id}")]
    public async Task<ApiResult<bool>> Delete(long id)
    {
        return await _userService.DeleteAsync(id);
    }

    [RequirePermission(SystemPermissions.UserResetPassword)]
    [HttpPost("{id}/reset-password")]
    public async Task<ApiResult<bool>> ResetPassword(long id, [FromBody] ResetPasswordRequest request)
    {
        return await _userService.ResetPasswordAsync(id, request.NewPassword);
    }

    [HttpGet("{userId}/organizations")]
    public async Task<ApiResult<List<UserOrganizationDto>>> GetUserOrganizations(long userId)
    {
        var result = await _userService.GetUserOrganizationsAsync(userId);
        return ApiResult<List<UserOrganizationDto>>.Success(result);
    }
}
