using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;
using STOTOP.Module.System.Filters;
using Microsoft.Extensions.Logging;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 成本方案管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/cost-plans")]
public class CostPlanController : ControllerBase
{
    private readonly ICostPlanService _costPlanService;
    private readonly ILogger<CostPlanController> _logger;

    public CostPlanController(ICostPlanService costPlanService, ILogger<CostPlanController> logger)
    {
        _costPlanService = costPlanService;
        _logger = logger;
    }

    /// <summary>
    /// 分页查询成本方案列表
    /// </summary>
    [HttpGet]
    [RequirePermission(ExpressPermissions.CostPlanView)]
    public async Task<ApiResult<PagedResult<CostPlanListDto>>> GetPlanList([FromQuery] CostPlanQueryRequest request)
    {
        var result = await _costPlanService.GetPlanListAsync(request);
        return ApiResult<PagedResult<CostPlanListDto>>.Success(result);
    }

    /// <summary>
    /// 获取成本方案详情
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(ExpressPermissions.CostPlanView)]
    public async Task<ApiResult<CostPlanDetailDto>> GetPlanById(long id)
    {
        var result = await _costPlanService.GetPlanByIdAsync(id);
        if (result == null)
            return ApiResult<CostPlanDetailDto>.Fail("成本方案不存在");
        return ApiResult<CostPlanDetailDto>.Success(result);
    }

    /// <summary>
    /// 创建成本方案
    /// </summary>
    [HttpPost]
    [RequirePermission(ExpressPermissions.CostPlanCreate)]
    public async Task<ApiResult<CostPlanDetailDto>> CreatePlan([FromBody] CreatePlanRequest request)
    {
        try
        {
            var result = await _costPlanService.CreatePlanAsync(request);
            return ApiResult<CostPlanDetailDto>.Success(result, "创建成本方案成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CostPlanDetailDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新成本方案
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(ExpressPermissions.CostPlanEdit)]
    public async Task<ApiResult<CostPlanDetailDto>> UpdatePlan(long id, [FromBody] UpdatePlanRequest request)
    {
        try
        {
            var result = await _costPlanService.UpdatePlanAsync(id, request);
            if (result == null)
                return ApiResult<CostPlanDetailDto>.Fail("成本方案不存在");
            return ApiResult<CostPlanDetailDto>.Success(result, "更新成本方案成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CostPlanDetailDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 启用方案
    /// </summary>
    [HttpPut("{id}/activate")]
    [RequirePermission(ExpressPermissions.CostPlanEdit)]
    public async Task<ApiResult> ActivatePlan(long id)
    {
        try
        {
            var ok = await _costPlanService.ActivatePlanAsync(id);
            if (!ok) return ApiResult.Fail("成本方案不存在");
            return ApiResult.Ok("方案已启用");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启用成本方案失败，ID={Id}", id);
            return ApiResult.Fail($"启用失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 停用方案
    /// </summary>
    [HttpPut("{id}/deactivate")]
    [RequirePermission(ExpressPermissions.CostPlanEdit)]
    public async Task<ApiResult> DeactivatePlan(long id)
    {
        try
        {
            var ok = await _costPlanService.DeactivatePlanAsync(id);
            if (!ok) return ApiResult.Fail("成本方案不存在");
            return ApiResult.Ok("方案已停用");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除成本方案
    /// </summary>
    [HttpDelete("{id}")]
    [RequirePermission(ExpressPermissions.CostPlanDelete)]
    public async Task<ApiResult> DeletePlan(long id)
    {
        try
        {
            var result = await _costPlanService.DeletePlanAsync(id);
            if (!result)
                return ApiResult.Fail("成本方案不存在");
            return ApiResult.Ok("删除成本方案成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    // === 成本项管理 ===

    /// <summary>
    /// 获取方案下所有成本项
    /// </summary>
    [HttpGet("{planId}/items")]
    [RequirePermission(ExpressPermissions.CostPlanView)]
    public async Task<ApiResult<List<CostPlanItemDto>>> GetItems(long planId)
    {
        var result = await _costPlanService.GetItemsAsync(planId);
        return ApiResult<List<CostPlanItemDto>>.Success(result);
    }

    /// <summary>
    /// 创建成本项
    /// </summary>
    [HttpPost("{planId}/items")]
    [RequirePermission(ExpressPermissions.CostPlanEdit)]
    public async Task<ApiResult<CostPlanItemDto>> CreateItem(long planId, [FromBody] CreateItemRequest request)
    {
        try
        {
            var result = await _costPlanService.CreateItemAsync(planId, request);
            return ApiResult<CostPlanItemDto>.Success(result, "创建成本项成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CostPlanItemDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新成本项
    /// </summary>
    [HttpPut("{planId}/items/{itemId}")]
    [RequirePermission(ExpressPermissions.CostPlanEdit)]
    public async Task<ApiResult<CostPlanItemDto>> UpdateItem(long planId, long itemId, [FromBody] UpdateItemRequest request)
    {
        try
        {
            var result = await _costPlanService.UpdateItemAsync(planId, itemId, request);
            if (result == null)
                return ApiResult<CostPlanItemDto>.Fail("成本项不存在");
            return ApiResult<CostPlanItemDto>.Success(result, "更新成本项成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CostPlanItemDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除成本项
    /// </summary>
    [HttpDelete("{planId}/items/{itemId}")]
    [RequirePermission(ExpressPermissions.CostPlanDelete)]
    public async Task<ApiResult> DeleteItem(long planId, long itemId)
    {
        try
        {
            var result = await _costPlanService.DeleteItemAsync(planId, itemId);
            if (!result)
                return ApiResult.Fail("成本项不存在");
            return ApiResult.Ok("删除成本项成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    // === 应用网点管理 ===

    /// <summary>
    /// 获取成本项的应用网点
    /// </summary>
    [HttpGet("{planId}/items/{itemId}/outlets")]
    [RequirePermission(ExpressPermissions.CostPlanView)]
    public async Task<ApiResult<List<long>>> GetItemOutlets(long planId, long itemId)
    {
        var result = await _costPlanService.GetItemOutletsAsync(planId, itemId);
        return ApiResult<List<long>>.Success(result);
    }

    /// <summary>
    /// 设置成本项的应用网点（全量替换）
    /// </summary>
    [HttpPut("{planId}/items/{itemId}/outlets")]
    [RequirePermission(ExpressPermissions.CostPlanEdit)]
    public async Task<ApiResult> SetItemOutlets(long planId, long itemId, [FromBody] List<long> outletIds)
    {
        try
        {
            await _costPlanService.SetItemOutletsAsync(planId, itemId, outletIds);
            return ApiResult.Ok("设置应用网点成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    // === 关联店铺管理（一口价专用）===

    /// <summary>
    /// 获取成本项的关联店铺
    /// </summary>
    [HttpGet("{planId}/items/{itemId}/shops")]
    [RequirePermission(ExpressPermissions.CostPlanView)]
    public async Task<ApiResult<List<string>>> GetItemShops(long planId, long itemId)
    {
        var result = await _costPlanService.GetItemShopsAsync(planId, itemId);
        return ApiResult<List<string>>.Success(result);
    }

    /// <summary>
    /// 设置成本项的关联店铺（全量替换）
    /// </summary>
    [HttpPut("{planId}/items/{itemId}/shops")]
    [RequirePermission(ExpressPermissions.CostPlanEdit)]
    public async Task<ApiResult> SetItemShops(long planId, long itemId, [FromBody] List<string> shopNames)
    {
        try
        {
            await _costPlanService.SetItemShopsAsync(planId, itemId, shopNames);
            return ApiResult.Ok("设置关联店铺成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    // === 时间段管理 ===

    /// <summary>
    /// 获取成本项的时间段列表
    /// </summary>
    [HttpGet("{planId}/items/{itemId}/periods")]
    [RequirePermission(ExpressPermissions.CostPlanView)]
    public async Task<ApiResult<List<CostPlanItemPeriodDto>>> GetPeriods(long planId, long itemId)
    {
        var result = await _costPlanService.GetPeriodsAsync(planId, itemId);
        return ApiResult<List<CostPlanItemPeriodDto>>.Success(result);
    }

    /// <summary>
    /// 创建时间段
    /// </summary>
    [HttpPost("{planId}/items/{itemId}/periods")]
    [RequirePermission(ExpressPermissions.CostPlanEdit)]
    public async Task<ApiResult<CostPlanItemPeriodDto>> CreatePeriod(long planId, long itemId, [FromBody] CreatePeriodRequest request)
    {
        try
        {
            var result = await _costPlanService.CreatePeriodAsync(planId, itemId, request);
            return ApiResult<CostPlanItemPeriodDto>.Success(result, "创建时间段成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CostPlanItemPeriodDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新时间段
    /// </summary>
    [HttpPut("{planId}/items/{itemId}/periods/{periodId}")]
    [RequirePermission(ExpressPermissions.CostPlanEdit)]
    public async Task<ApiResult<CostPlanItemPeriodDto>> UpdatePeriod(long planId, long itemId, long periodId, [FromBody] UpdatePeriodRequest request)
    {
        try
        {
            var result = await _costPlanService.UpdatePeriodAsync(planId, itemId, periodId, request);
            if (result == null)
                return ApiResult<CostPlanItemPeriodDto>.Fail("时间段不存在");
            return ApiResult<CostPlanItemPeriodDto>.Success(result, "更新时间段成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CostPlanItemPeriodDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除时间段
    /// </summary>
    [HttpDelete("{planId}/items/{itemId}/periods/{periodId}")]
    [RequirePermission(ExpressPermissions.CostPlanDelete)]
    public async Task<ApiResult> DeletePeriod(long planId, long itemId, long periodId)
    {
        var result = await _costPlanService.DeletePeriodAsync(planId, itemId, periodId);
        if (!result)
            return ApiResult.Fail("时间段不存在");
        return ApiResult.Ok("删除时间段成功");
    }

    // === 矩阵保存/读取 ===

    /// <summary>
    /// 保存成本项矩阵（支持 national/province/city 三种模式）
    /// </summary>
    [HttpPut("{planId}/items/{itemId}/matrix")]
    [RequirePermission(ExpressPermissions.CostPlanEdit)]
    public async Task<ApiResult> SaveItemMatrix(long planId, long itemId, [FromBody] SaveItemMatrixRequest request)
    {
        try
        {
            await _costPlanService.SaveItemMatrixAsync(planId, itemId, request);
            return ApiResult.Ok("保存矩阵成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取成本项矩阵
    /// </summary>
    [HttpGet("{planId}/items/{itemId}/matrix")]
    [RequirePermission(ExpressPermissions.CostPlanView)]
    public async Task<ApiResult<CostItemMatrixDto>> GetItemMatrix(long planId, long itemId, [FromQuery] DateTime? effectiveDate)
    {
        var result = await _costPlanService.GetItemMatrixAsync(planId, itemId, effectiveDate);
        if (result == null)
            return ApiResult<CostItemMatrixDto>.Fail("成本项不存在");
        return ApiResult<CostItemMatrixDto>.Success(result);
    }

    // === 互斥配置管理 ===

    /// <summary>
    /// 获取方案的互斥配置列表
    /// </summary>
    [HttpGet("{planId}/exclusions")]
    [RequirePermission(ExpressPermissions.CostPlanView)]
    public async Task<ApiResult<List<CostPlanExclusionDto>>> GetExclusions(long planId)
    {
        var result = await _costPlanService.GetExclusionsAsync(planId);
        return ApiResult<List<CostPlanExclusionDto>>.Success(result);
    }

    /// <summary>
    /// 创建互斥配置
    /// </summary>
    [HttpPost("{planId}/exclusions")]
    [RequirePermission(ExpressPermissions.CostPlanEdit)]
    public async Task<ApiResult<CostPlanExclusionDto>> CreateExclusion(long planId, [FromBody] CreateExclusionRequest request)
    {
        try
        {
            var result = await _costPlanService.CreateExclusionAsync(planId, request);
            return ApiResult<CostPlanExclusionDto>.Success(result, "创建互斥配置成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CostPlanExclusionDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新互斥配置
    /// </summary>
    [HttpPut("{planId}/exclusions/{exclusionId}")]
    [RequirePermission(ExpressPermissions.CostPlanEdit)]
    public async Task<ApiResult<CostPlanExclusionDto>> UpdateExclusion(long planId, long exclusionId, [FromBody] UpdateExclusionRequest request)
    {
        try
        {
            var result = await _costPlanService.UpdateExclusionAsync(planId, exclusionId, request);
            if (result == null)
                return ApiResult<CostPlanExclusionDto>.Fail("互斥配置不存在");
            return ApiResult<CostPlanExclusionDto>.Success(result, "更新互斥配置成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CostPlanExclusionDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除互斥配置
    /// </summary>
    [HttpDelete("{planId}/exclusions/{exclusionId}")]
    [RequirePermission(ExpressPermissions.CostPlanDelete)]
    public async Task<ApiResult> DeleteExclusion(long planId, long exclusionId)
    {
        var result = await _costPlanService.DeleteExclusionAsync(planId, exclusionId);
        if (!result)
            return ApiResult.Fail("互斥配置不存在");
        return ApiResult.Ok("删除互斥配置成功");
    }

    // === 运单成本计算 ===

    /// <summary>
    /// 计算运单有效成本
    /// </summary>
    [HttpGet("effective-cost")]
    [RequirePermission(ExpressPermissions.CostPlanView)]
    public async Task<ApiResult<EffectiveCostResult>> GetEffectiveCost(
        [FromQuery] string brandCode,
        [FromQuery] long outletId,
        [FromQuery] string? shopName,
        [FromQuery] DateTime businessDate)
    {
        var result = await _costPlanService.GetEffectiveCostAsync(brandCode, outletId, shopName, businessDate);
        if (result == null)
            return ApiResult<EffectiveCostResult>.Fail("未找到匹配的成本方案");
        return ApiResult<EffectiveCostResult>.Success(result);
    }

    // === 城市查询 ===

    /// <summary>
    /// 搜索城市列表
    /// </summary>
    [HttpGet("/api/express/cities")]
    public async Task<ApiResult<List<CityDto>>> GetCities([FromQuery] string? keyword)
    {
        var cities = await _costPlanService.GetCitiesAsync(keyword);
        return ApiResult<List<CityDto>>.Success(cities);
    }
}
