using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Filters;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

[Route("api/system/organizations")]
[ApiController]
[Authorize]
public class OrganizationController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly STOTOPDbContext _context;
    private readonly IOrgContextService _orgContextService;

    public OrganizationController(IOrganizationService organizationService, STOTOPDbContext context, IOrgContextService orgContextService)
    {
        _organizationService = organizationService;
        _context = context;
        _orgContextService = orgContextService;
    }

    [HttpGet("tree")]
    public async Task<ApiResult<List<OrganizationDto>>> GetTree()
    {
        return await _organizationService.GetTreeAsync();
    }

    [RequirePermission(SystemPermissions.OrgCreate)]
    [HttpPost]
    public async Task<ApiResult<OrganizationDto>> Create([FromBody] CreateOrganizationRequest request)
    {
        return await _organizationService.CreateAsync(request);
    }

    [RequirePermission(SystemPermissions.OrgEdit)]
    [HttpPut("{id}")]
    public async Task<ApiResult<OrganizationDto>> Update(long id, [FromBody] UpdateOrganizationRequest request)
    {
        return await _organizationService.UpdateAsync(id, request);
    }

    [RequirePermission(SystemPermissions.OrgDelete)]
    [HttpDelete("{id}")]
    public async Task<ApiResult<bool>> Delete(long id)
    {
        return await _organizationService.DeleteAsync(id);
    }

    [HttpGet("chart")]
    public async Task<ApiResult<List<OrganizationDto>>> GetOrgChart()
    {
        return await _organizationService.GetOrgChartAsync();
    }

    [HttpGet("{orgId}/account-sets")]
    public async Task<IActionResult> GetOrgAccountSets(long orgId)
    {
        // 校验当前用户是否属于该组织
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResult.Fail("无法识别用户身份", 401));

        var userOrgs = await _orgContextService.GetUserOrganizationsAsync(userId);
        if (!userOrgs.Any(o => o.OrgId == orgId))
            return StatusCode(403, ApiResult.Fail("无权访问该组织的账套信息", 403));

        var accountSets = await _context.Database
            .SqlQueryRaw<OrgAccountSetDto>(
                @"SELECT [FID] AS [Id], [F名称] AS [Name], [F编码] AS [Code], [F状态] AS [Status]
                  FROM [FIN账套]
                  WHERE [F组织ID] = {0}", orgId)
            .ToListAsync();

        return Ok(ApiResult<List<OrgAccountSetDto>>.Success(accountSets));
    }

    /// <summary>获取全部启用部门（扁平列表，供辅助核算关联使用）</summary>
    [HttpGet("departments")]
    public async Task<ApiResult<List<object>>> GetDepartments()
    {
        var items = await _context.Set<SysOrganization>()
            .Include(o => o.OrgType)
            .Where(o => o.FStatus == 1 && o.OrgType != null && o.OrgType.FCode == "DEPT")
            .OrderBy(o => o.FSort)
            .Select(o => (object)new { id = o.FID, code = o.FCode, name = o.FName, status = o.FStatus })
            .ToListAsync();
        return ApiResult<List<object>>.Success(items);
    }
}
