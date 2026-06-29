using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 品牌管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/brands")]
public class BrandController : ControllerBase
{
    private readonly IBrandService _brandService;

    public BrandController(IBrandService brandService)
    {
        _brandService = brandService;
    }

    /// <summary>
    /// 获取品牌分页列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResult<PagedResult<BrandListItemDto>>> GetList([FromQuery] BrandQueryRequest request)
    {
        var result = await _brandService.GetListAsync(request);
        return ApiResult<PagedResult<BrandListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取品牌详情
    /// </summary>
    [HttpGet("{code}")]
    public async Task<ApiResult<BrandDto>> GetByCode(string code)
    {
        var result = await _brandService.GetByCodeAsync(code);
        if (result == null)
            return ApiResult<BrandDto>.Fail("品牌不存在");
        return ApiResult<BrandDto>.Success(result);
    }

    /// <summary>
    /// 创建品牌
    /// </summary>
    [HttpPost]
    public async Task<ApiResult<BrandDto>> Create([FromBody] CreateBrandRequest request)
    {
        try
        {
            var result = await _brandService.CreateAsync(request);
            return ApiResult<BrandDto>.Success(result, "创建品牌成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BrandDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新品牌
    /// </summary>
    [HttpPut("{code}")]
    public async Task<ApiResult<BrandDto>> Update(string code, [FromBody] UpdateBrandRequest request)
    {
        var result = await _brandService.UpdateAsync(code, request);
        if (result == null)
            return ApiResult<BrandDto>.Fail("品牌不存在");
        return ApiResult<BrandDto>.Success(result, "更新品牌成功");
    }

    /// <summary>
    /// 删除品牌
    /// </summary>
    [HttpDelete("{code}")]
    public async Task<ApiResult> Delete(string code)
    {
        try
        {
            var result = await _brandService.DeleteAsync(code);
            if (!result)
                return ApiResult.Fail("品牌不存在");
            return ApiResult.Ok("删除品牌成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
