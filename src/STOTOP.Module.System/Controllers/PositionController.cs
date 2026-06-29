using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

[Route("api/system/positions")]
[ApiController]
[Authorize]
public class PositionController : ControllerBase
{
    private readonly IPositionService _positionService;

    public PositionController(IPositionService positionService)
    {
        _positionService = positionService;
    }

    [HttpGet]
    public async Task<ApiResult<object>> GetList([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
    {
        var (items, total) = await _positionService.GetPagedListAsync(pageIndex, pageSize, keyword);
        return ApiResult<object>.Success(new { items, total });
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<PositionDto?>> GetById(long id)
    {
        var result = await _positionService.GetByIdAsync(id);
        if (result == null)
            return ApiResult<PositionDto?>.Fail("岗位不存在", 404);
        return ApiResult<PositionDto?>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<PositionDto>> Create([FromBody] CreatePositionRequest request)
    {
        var result = await _positionService.CreateAsync(request);
        return ApiResult<PositionDto>.Success(result, "创建成功");
    }

    [HttpPut("{id}")]
    public async Task<ApiResult> Update(long id, [FromBody] UpdatePositionRequest request)
    {
        await _positionService.UpdateAsync(id, request);
        return ApiResult.Ok("更新成功");
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        await _positionService.DeleteAsync(id);
        return ApiResult.Ok("删除成功");
    }

    [HttpPost("{id}/organizations")]
    public async Task<ApiResult> AssignOrganizations(long id, [FromBody] AssignPositionOrganizationsRequest request)
    {
        await _positionService.AssignOrganizationsAsync(id, request.OrganizationIds);
        return ApiResult.Ok("分配成功");
    }

    [HttpPost("{id}/users")]
    public async Task<ApiResult> AssignUsers(long id, [FromBody] AssignPositionUsersRequest request)
    {
        await _positionService.AssignUsersAsync(id, request.UserIds);
        return ApiResult.Ok("分配成功");
    }

    [HttpGet("by-organization/{orgId}")]
    public async Task<ApiResult<List<PositionDto>>> GetByOrganization(long orgId)
    {
        var result = await _positionService.GetByOrganizationAsync(orgId);
        return ApiResult<List<PositionDto>>.Success(result);
    }

    [HttpGet("by-user/{userId}")]
    public async Task<ApiResult<List<PositionDto>>> GetByUser(long userId)
    {
        var result = await _positionService.GetByUserAsync(userId);
        return ApiResult<List<PositionDto>>.Success(result);
    }
}
