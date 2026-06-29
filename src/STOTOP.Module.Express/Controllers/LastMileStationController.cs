using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 末端驿站管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/last-mile-stations")]
public class LastMileStationController : ControllerBase
{
    private readonly ILastMileStationService _service;

    public LastMileStationController(ILastMileStationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<LastMileStationDto>>> GetList([FromQuery] LastMileStationQueryRequest request)
    {
        var result = await _service.GetListAsync(request);
        return ApiResult<PagedResult<LastMileStationDto>>.Success(result);
    }

    [HttpGet("{code}")]
    public async Task<ApiResult<LastMileStationDto>> GetByCode(string code)
    {
        var result = await _service.GetByIdAsync(code);
        if (result == null)
            return ApiResult<LastMileStationDto>.Fail("末端驿站不存在");
        return ApiResult<LastMileStationDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<LastMileStationDto>> Create([FromBody] CreateLastMileStationRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return ApiResult<LastMileStationDto>.Success(result, "创建末端驿站成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<LastMileStationDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{code}")]
    public async Task<ApiResult<LastMileStationDto>> Update(string code, [FromBody] UpdateLastMileStationRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(code, request);
            if (result == null)
                return ApiResult<LastMileStationDto>.Fail("末端驿站不存在");
            return ApiResult<LastMileStationDto>.Success(result, "更新末端驿站成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<LastMileStationDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{code}")]
    public async Task<ApiResult> Delete(string code)
    {
        var result = await _service.DeleteAsync(code);
        if (!result)
            return ApiResult.Fail("末端驿站不存在");
        return ApiResult.Ok("删除末端驿站成功");
    }
}
