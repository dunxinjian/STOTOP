using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Controllers;

[Authorize]
[ApiController]
[Route("api/dormitory/rooms/{roomId}/facilities")]
public class FacilityController : ControllerBase
{
    private readonly IFacilityService _facilityService;

    public FacilityController(IFacilityService facilityService)
    {
        _facilityService = facilityService;
    }

    [HttpGet]
    public async Task<ApiResult<List<FacilityDto>>> GetList(long roomId)
    {
        var result = await _facilityService.GetFacilitiesByRoomIdAsync(roomId);
        return ApiResult<List<FacilityDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<FacilityDto>> GetById(long roomId, long id)
    {
        var result = await _facilityService.GetFacilityByIdAsync(id);
        if (result == null)
        {
            return ApiResult<FacilityDto>.Fail("设施不存在");
        }
        return ApiResult<FacilityDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<FacilityDto>> Create(long roomId, [FromBody] CreateFacilityRequest request)
    {
        try
        {
            var result = await _facilityService.CreateFacilityAsync(roomId, request);
            return ApiResult<FacilityDto>.Success(result, "创建设施成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<FacilityDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<FacilityDto>> Update(long roomId, long id, [FromBody] UpdateFacilityRequest request)
    {
        var result = await _facilityService.UpdateFacilityAsync(id, request);
        if (result == null)
        {
            return ApiResult<FacilityDto>.Fail("设施不存在");
        }
        return ApiResult<FacilityDto>.Success(result, "更新设施成功");
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long roomId, long id)
    {
        var result = await _facilityService.DeleteFacilityAsync(id);
        if (!result)
        {
            return ApiResult.Fail("设施不存在");
        }
        return ApiResult.Ok("删除设施成功");
    }
}
