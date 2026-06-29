using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 附加费管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/price-surcharges")]
public class PriceSurchargeController : ControllerBase
{
    private readonly IPriceSurchargeService _surchargeService;

    public PriceSurchargeController(IPriceSurchargeService surchargeService)
    {
        _surchargeService = surchargeService;
    }

    /// <summary>
    /// 分页查询附加费列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResult<PagedResult<PriceSurchargeListItemDto>>> GetList([FromQuery] PriceSurchargeQueryRequest request)
    {
        var result = await _surchargeService.GetListAsync(request);
        return ApiResult<PagedResult<PriceSurchargeListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取附加费详情（含配置项和目的地）
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResult<PriceSurchargeDto>> GetById(long id)
    {
        var result = await _surchargeService.GetByIdAsync(id);
        if (result == null)
            return ApiResult<PriceSurchargeDto>.Fail("附加费不存在");
        return ApiResult<PriceSurchargeDto>.Success(result);
    }

    /// <summary>
    /// 创建附加费
    /// </summary>
    [HttpPost]
    public async Task<ApiResult<PriceSurchargeDto>> Create([FromBody] CreatePriceSurchargeRequest request)
    {
        try
        {
            var result = await _surchargeService.CreateAsync(request);
            return ApiResult<PriceSurchargeDto>.Success(result, "创建附加费成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<PriceSurchargeDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新附加费
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ApiResult<PriceSurchargeDto>> Update(long id, [FromBody] UpdatePriceSurchargeRequest request)
    {
        try
        {
            var result = await _surchargeService.UpdateAsync(id, request);
            if (result == null)
                return ApiResult<PriceSurchargeDto>.Fail("附加费不存在");
            return ApiResult<PriceSurchargeDto>.Success(result, "更新附加费成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<PriceSurchargeDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除附加费
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _surchargeService.DeleteAsync(id);
        if (!result)
            return ApiResult.Fail("附加费不存在");
        return ApiResult.Ok("删除附加费成功");
    }

    /// <summary>
    /// 切换启用/停用状态
    /// </summary>
    [HttpPut("{id}/toggle-active")]
    public async Task<ApiResult> ToggleActive(long id)
    {
        var result = await _surchargeService.ToggleActiveAsync(id);
        if (!result)
            return ApiResult.Fail("附加费不存在");
        return ApiResult.Ok("操作成功");
    }
}
