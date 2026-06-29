using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Controllers;

[Authorize]
[ApiController]
[Route("api/dormitory/rooms/{roomId}/beds")]
public class BedController : ControllerBase
{
    private readonly IBedService _bedService;

    public BedController(IBedService bedService)
    {
        _bedService = bedService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<BedListItemDto>>> GetList(long roomId, [FromQuery] BedQueryRequest request)
    {
        var result = await _bedService.GetBedsAsync(roomId, request);
        return ApiResult<PagedResult<BedListItemDto>>.Success(result);
    }

    [HttpGet("all")]
    public async Task<ApiResult<List<BedListItemDto>>> GetAllEnabled(long roomId)
    {
        var result = await _bedService.GetAllEnabledBedsAsync(roomId);
        return ApiResult<List<BedListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<BedDto>> GetById(long roomId, long id)
    {
        var result = await _bedService.GetBedByIdAsync(id);
        if (result == null)
        {
            return ApiResult<BedDto>.Fail("床位不存在");
        }
        return ApiResult<BedDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<BedDto>> Create(long roomId, [FromBody] CreateBedRequest request)
    {
        try
        {
            var result = await _bedService.CreateBedAsync(roomId, request);
            return ApiResult<BedDto>.Success(result, "创建床位成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BedDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<BedDto>> Update(long roomId, long id, [FromBody] UpdateBedRequest request)
    {
        try
        {
            var result = await _bedService.UpdateBedAsync(id, request);
            if (result == null)
            {
                return ApiResult<BedDto>.Fail("床位不存在");
            }
            return ApiResult<BedDto>.Success(result, "更新床位成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BedDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long roomId, long id)
    {
        var result = await _bedService.DeleteBedAsync(id);
        if (!result)
        {
            return ApiResult.Fail("床位不存在");
        }
        return ApiResult.Ok("删除床位成功");
    }

    [HttpPut("{id}/status")]
    public async Task<ApiResult> UpdateStatus(long roomId, long id, [FromBody] UpdateBedStatusRequest request)
    {
        var result = await _bedService.UpdateStatusAsync(id, request.Status);
        if (!result)
        {
            return ApiResult.Fail("床位不存在");
        }
        return ApiResult.Ok("更新状态成功");
    }

    [HttpGet("check-number")]
    public async Task<ApiResult<bool>> CheckNumber(long roomId, [FromQuery] string bedNumber, [FromQuery] long excludeId = 0)
    {
        var exists = await _bedService.CheckBedNumberExistsAsync(roomId, bedNumber, excludeId);
        return ApiResult<bool>.Success(exists);
    }
}
