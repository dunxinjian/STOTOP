using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Controllers;

[Authorize]
[ApiController]
[Route("api/dormitory/buildings")]
public class BuildingController : ControllerBase
{
    private readonly IBuildingService _buildingService;

    public BuildingController(IBuildingService buildingService)
    {
        _buildingService = buildingService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<BuildingListItemDto>>> GetList([FromQuery] BuildingQueryRequest request)
    {
        var result = await _buildingService.GetBuildingsAsync(request);
        return ApiResult<PagedResult<BuildingListItemDto>>.Success(result);
    }

    [HttpGet("all")]
    public async Task<ApiResult<List<BuildingListItemDto>>> GetAllEnabled()
    {
        var result = await _buildingService.GetAllEnabledBuildingsAsync();
        return ApiResult<List<BuildingListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<BuildingDto>> GetById(long id)
    {
        var result = await _buildingService.GetBuildingByIdAsync(id);
        if (result == null)
        {
            return ApiResult<BuildingDto>.Fail("楼栋不存在");
        }
        return ApiResult<BuildingDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<BuildingDto>> Create([FromBody] CreateBuildingRequest request)
    {
        try
        {
            var result = await _buildingService.CreateBuildingAsync(request);
            return ApiResult<BuildingDto>.Success(result, "创建楼栋成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BuildingDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<BuildingDto>> Update(long id, [FromBody] UpdateBuildingRequest request)
    {
        try
        {
            var result = await _buildingService.UpdateBuildingAsync(id, request);
            if (result == null)
            {
                return ApiResult<BuildingDto>.Fail("楼栋不存在");
            }
            return ApiResult<BuildingDto>.Success(result, "更新楼栋成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BuildingDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _buildingService.DeleteBuildingAsync(id);
        if (!result)
        {
            return ApiResult.Fail("楼栋不存在");
        }
        return ApiResult.Ok("删除楼栋成功");
    }

    [HttpPut("{id}/status")]
    public async Task<ApiResult> UpdateStatus(long id, [FromBody] UpdateBuildingStatusRequest request)
    {
        var result = await _buildingService.UpdateStatusAsync(id, request.Status);
        if (!result)
        {
            return ApiResult.Fail("楼栋不存在");
        }
        return ApiResult.Ok("更新状态成功");
    }

    [HttpGet("check-code")]
    public async Task<ApiResult<bool>> CheckCode([FromQuery] string code, [FromQuery] long excludeId = 0)
    {
        var exists = await _buildingService.CheckCodeExistsAsync(code, excludeId);
        return ApiResult<bool>.Success(exists);
    }
}
