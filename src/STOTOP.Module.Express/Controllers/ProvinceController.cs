using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 省份与大区管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/provinces")]
public class ProvinceController : ControllerBase
{
    private readonly IProvinceService _provinceService;

    public ProvinceController(IProvinceService provinceService)
    {
        _provinceService = provinceService;
    }

    /// <summary>
    /// 获取全部省份列表（用于下拉选择）
    /// </summary>
    [HttpGet]
    public async Task<ApiResult<List<ProvinceListItemDto>>> GetAll()
    {
        var result = await _provinceService.GetAllAsync();
        return ApiResult<List<ProvinceListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取省份详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResult<ProvinceDto>> GetById(int id)
    {
        var result = await _provinceService.GetByIdAsync(id);
        if (result == null)
            return ApiResult<ProvinceDto>.Fail("省份不存在");
        return ApiResult<ProvinceDto>.Success(result);
    }

    /// <summary>
    /// 创建省份
    /// </summary>
    [HttpPost]
    public async Task<ApiResult<ProvinceDto>> Create([FromBody] CreateProvinceRequest request)
    {
        try
        {
            var result = await _provinceService.CreateAsync(request);
            return ApiResult<ProvinceDto>.Success(result, "创建省份成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ProvinceDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新省份
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ApiResult<ProvinceDto>> Update(int id, [FromBody] UpdateProvinceRequest request)
    {
        try
        {
            var result = await _provinceService.UpdateAsync(id, request);
            if (result == null)
                return ApiResult<ProvinceDto>.Fail("省份不存在");
            return ApiResult<ProvinceDto>.Success(result, "更新省份成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ProvinceDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除省份
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(int id)
    {
        var result = await _provinceService.DeleteAsync(id);
        if (!result)
            return ApiResult.Fail("省份不存在");
        return ApiResult.Ok("删除省份成功");
    }

    /// <summary>
    /// 获取所有大区名称列表
    /// </summary>
    [HttpGet("regions")]
    public async Task<ApiResult<List<string>>> GetRegions()
    {
        var result = await _provinceService.GetRegionsAsync();
        return ApiResult<List<string>>.Success(result);
    }

    /// <summary>
    /// 重命名大区
    /// </summary>
    [HttpPut("regions/rename")]
    public async Task<ApiResult<int>> RenameRegion([FromBody] RenameRegionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OldName) || string.IsNullOrWhiteSpace(request.NewName))
            return ApiResult<int>.Fail("大区名称不能为空");
        var count = await _provinceService.RenameRegionAsync(request.OldName, request.NewName);
        return ApiResult<int>.Success(count, $"已更新 {count} 个省份的大区名称");
    }
}
