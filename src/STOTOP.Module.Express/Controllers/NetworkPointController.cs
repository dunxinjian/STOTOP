using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 网点管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/network-points")]
public class NetworkPointController : ControllerBase
{
    private readonly INetworkPointService _service;

    public NetworkPointController(INetworkPointService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<NetworkPointDto>>> GetList([FromQuery] NetworkPointQueryRequest request)
    {
        var result = await _service.GetListAsync(request);
        return ApiResult<PagedResult<NetworkPointDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<NetworkPointDto>> GetById(string id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return ApiResult<NetworkPointDto>.Fail("网点不存在");
        return ApiResult<NetworkPointDto>.Success(result);
    }

    [HttpGet("check-code")]
    public async Task<ApiResult<bool>> CheckCodeExists([FromQuery] string code)
    {
        var exists = await _service.CheckCodeExistsAsync(code);
        return ApiResult<bool>.Success(exists);
    }

    [HttpPost]
    public async Task<ApiResult<NetworkPointDto>> Create([FromBody] CreateNetworkPointRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return ApiResult<NetworkPointDto>.Success(result, "创建网点成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<NetworkPointDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<NetworkPointDto>> Update(string id, [FromBody] UpdateNetworkPointRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request);
            if (result == null)
                return ApiResult<NetworkPointDto>.Fail("网点不存在");
            return ApiResult<NetworkPointDto>.Success(result, "更新网点成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<NetworkPointDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(string id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result)
            return ApiResult.Fail("网点不存在");
        return ApiResult.Ok("删除网点成功");
    }
}
