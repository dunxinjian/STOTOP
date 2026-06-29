using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 用户网点权限管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/network-permissions")]
public class UserNetworkPermissionController : ControllerBase
{
    private readonly IUserNetworkPermissionService _service;

    public UserNetworkPermissionController(IUserNetworkPermissionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<UserNetworkPermissionDto>>> GetList([FromQuery] UserNetworkPermissionQueryRequest request)
    {
        var result = await _service.GetListAsync(request);
        return ApiResult<PagedResult<UserNetworkPermissionDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<UserNetworkPermissionDto>> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return ApiResult<UserNetworkPermissionDto>.Fail("权限记录不存在");
        return ApiResult<UserNetworkPermissionDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<UserNetworkPermissionDto>> Create([FromBody] CreateUserNetworkPermissionRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return ApiResult<UserNetworkPermissionDto>.Success(result, "创建网点权限成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<UserNetworkPermissionDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result)
            return ApiResult.Fail("权限记录不存在");
        return ApiResult.Ok("删除网点权限成功");
    }
}
