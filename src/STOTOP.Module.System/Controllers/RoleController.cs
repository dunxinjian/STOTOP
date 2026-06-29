using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Filters;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

[Route("api/system/roles")]
[ApiController]
[Authorize]
public class RoleController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RoleController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<ApiResult<List<RoleDto>>> GetAll()
    {
        return await _roleService.GetAllAsync();
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<RoleDto>> GetById(long id)
    {
        return await _roleService.GetByIdAsync(id);
    }

    [RequirePermission(SystemPermissions.RoleCreate)]
    [HttpPost]
    public async Task<ApiResult<RoleDto>> Create([FromBody] CreateRoleRequest request)
    {
        return await _roleService.CreateAsync(request);
    }

    [RequirePermission(SystemPermissions.RoleEdit)]
    [HttpPut("{id}")]
    public async Task<ApiResult<RoleDto>> Update(long id, [FromBody] UpdateRoleRequest request)
    {
        return await _roleService.UpdateAsync(id, request);
    }

    [RequirePermission(SystemPermissions.RoleDelete)]
    [HttpDelete("{id}")]
    public async Task<ApiResult<bool>> Delete(long id)
    {
        return await _roleService.DeleteAsync(id);
    }

    [RequirePermission(SystemPermissions.RoleAssignPermission)]
    [HttpPost("{id}/permissions")]
    public async Task<ApiResult<bool>> AssignPermissions(long id, [FromBody] AssignPermissionsRequest request)
    {
        return await _roleService.AssignPermissionsAsync(id, request.PermissionIds);
    }
}
