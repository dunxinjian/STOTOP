using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CRM.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/orgs")]
public class CrmOrgController : ControllerBase
{
    private readonly ICrmOrgService _orgService;

    public CrmOrgController(ICrmOrgService orgService)
    {
        _orgService = orgService;
    }

    [HttpGet("role-mappings")]
    [RequirePermission(CrmPermissions.GroupView)]
    public async Task<ApiResult<PagedResult<CrmRoleMappingListItemDto>>> GetRoleMappings([FromQuery] RoleMappingQueryRequest request)
    {
        var result = await _orgService.GetRoleMappingsAsync(request);
        return ApiResult<PagedResult<CrmRoleMappingListItemDto>>.Success(result);
    }

    [HttpGet("role-mappings/{id}")]
    [RequirePermission(CrmPermissions.GroupView)]
    public async Task<ApiResult<CrmRoleMappingDto>> GetRoleMappingById(long id)
    {
        var result = await _orgService.GetRoleMappingByIdAsync(id);
        if (result == null)
            return ApiResult<CrmRoleMappingDto>.Fail("角色映射不存在");
        return ApiResult<CrmRoleMappingDto>.Success(result);
    }

    [HttpPost("role-mappings")]
    [RequirePermission(CrmPermissions.GroupManage)]
    public async Task<ApiResult<CrmRoleMappingDto>> CreateRoleMapping([FromBody] CreateRoleMappingRequest request)
    {
        try
        {
            var result = await _orgService.CreateRoleMappingAsync(request);
            return ApiResult<CrmRoleMappingDto>.Success(result, "创建角色映射成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CrmRoleMappingDto>.Fail(ex.Message);
        }
    }

    [HttpPut("role-mappings/{id}")]
    [RequirePermission(CrmPermissions.GroupManage)]
    public async Task<ApiResult<CrmRoleMappingDto>> UpdateRoleMapping(long id, [FromBody] UpdateRoleMappingRequest request)
    {
        var result = await _orgService.UpdateRoleMappingAsync(id, request);
        if (result == null)
            return ApiResult<CrmRoleMappingDto>.Fail("角色映射不存在");
        return ApiResult<CrmRoleMappingDto>.Success(result, "更新角色映射成功");
    }

    [HttpDelete("role-mappings/{id}")]
    [RequirePermission(CrmPermissions.GroupManage)]
    public async Task<ApiResult> DeleteRoleMapping(long id)
    {
        var result = await _orgService.DeleteRoleMappingAsync(id);
        if (!result)
            return ApiResult.Fail("角色映射不存在");
        return ApiResult.Ok("删除角色映射成功");
    }

    [HttpGet("{orgId}/bd-list")]
    [RequirePermission(CrmPermissions.GroupView)]
    public async Task<ApiResult<List<CrmRoleMappingListItemDto>>> GetBdList(long orgId)
    {
        var result = await _orgService.GetBdListAsync(orgId);
        return ApiResult<List<CrmRoleMappingListItemDto>>.Success(result);
    }

    [HttpGet("{orgId}/maintenance-list")]
    [RequirePermission(CrmPermissions.GroupView)]
    public async Task<ApiResult<List<CrmRoleMappingListItemDto>>> GetMaintenanceList(long orgId)
    {
        var result = await _orgService.GetMaintenanceListAsync(orgId);
        return ApiResult<List<CrmRoleMappingListItemDto>>.Success(result);
    }
}
