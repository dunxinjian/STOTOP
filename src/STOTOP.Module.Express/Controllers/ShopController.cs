using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 店铺管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/shops")]
public class ShopController : ControllerBase
{
    private readonly IShopService _shopService;

    public ShopController(IShopService shopService)
    {
        _shopService = shopService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<ShopListItemDto>>> GetList([FromQuery] ShopQueryRequest request)
    {
        var result = await _shopService.GetListAsync(request);
        return ApiResult<PagedResult<ShopListItemDto>>.Success(result);
    }

    [HttpGet("{name}")]
    public async Task<ApiResult<ShopDto>> GetByName(string name)
    {
        var result = await _shopService.GetByNameAsync(name);
        if (result == null)
            return ApiResult<ShopDto>.Fail("店铺不存在");
        return ApiResult<ShopDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<ShopDto>> Create([FromBody] CreateShopRequest request)
    {
        try
        {
            var result = await _shopService.CreateAsync(request);
            return ApiResult<ShopDto>.Success(result, "创建店铺成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ShopDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{name}")]
    public async Task<ApiResult<ShopDto>> Update(string name, [FromBody] UpdateShopRequest request)
    {
        try
        {
            var result = await _shopService.UpdateAsync(name, request);
            if (result == null)
                return ApiResult<ShopDto>.Fail("店铺不存在");
            return ApiResult<ShopDto>.Success(result, "更新店铺成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ShopDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{name}")]
    public async Task<ApiResult> Delete(string name)
    {
        var result = await _shopService.DeleteAsync(name);
        if (!result)
            return ApiResult.Fail("店铺不存在");
        return ApiResult.Ok("删除店铺成功");
    }

    /// <summary>
    /// 添加店铺归属
    /// </summary>
    [HttpPost("assignments")]
    public async Task<ApiResult<ShopAssignmentDto>> AddAssignment([FromBody] CreateShopAssignmentRequest request)
    {
        var result = await _shopService.AddAssignmentAsync(request);
        return ApiResult<ShopAssignmentDto>.Success(result, "添加归属成功");
    }

    /// <summary>
    /// 删除店铺归属
    /// </summary>
    [HttpDelete("assignments/{assignmentId}")]
    public async Task<ApiResult> RemoveAssignment(long assignmentId)
    {
        var result = await _shopService.RemoveAssignmentAsync(assignmentId);
        if (!result)
            return ApiResult.Fail("归属记录不存在");
        return ApiResult.Ok("删除归属成功");
    }
}
