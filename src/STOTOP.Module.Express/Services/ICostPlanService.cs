using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 成本方案服务接口
/// </summary>
public interface ICostPlanService
{
    // === 方案管理 ===
    Task<PagedResult<CostPlanListDto>> GetPlanListAsync(CostPlanQueryRequest request);
    Task<CostPlanDetailDto?> GetPlanByIdAsync(long id);
    Task<CostPlanDetailDto> CreatePlanAsync(CreatePlanRequest request);
    Task<CostPlanDetailDto?> UpdatePlanAsync(long id, UpdatePlanRequest request);
    Task<bool> DeletePlanAsync(long id);
    Task<bool> ActivatePlanAsync(long id);
    Task<bool> DeactivatePlanAsync(long id);

    // === 成本项管理 ===
    Task<List<CostPlanItemDto>> GetItemsAsync(long planId);
    Task<CostPlanItemDto> CreateItemAsync(long planId, CreateItemRequest request);
    Task<CostPlanItemDto?> UpdateItemAsync(long planId, long itemId, UpdateItemRequest request);
    Task<bool> DeleteItemAsync(long planId, long itemId);

    // === 应用网点管理 ===
    Task<List<long>> GetItemOutletsAsync(long planId, long itemId);
    Task SetItemOutletsAsync(long planId, long itemId, List<long> outletIds);

    // === 关联店铺管理（一口价专用）===
    Task<List<string>> GetItemShopsAsync(long planId, long itemId);
    Task SetItemShopsAsync(long planId, long itemId, List<string> shopNames);

    // === 时间段管理 ===
    Task<List<CostPlanItemPeriodDto>> GetPeriodsAsync(long planId, long itemId);
    Task<CostPlanItemPeriodDto> CreatePeriodAsync(long planId, long itemId, CreatePeriodRequest request);
    Task<CostPlanItemPeriodDto?> UpdatePeriodAsync(long planId, long itemId, long periodId, UpdatePeriodRequest request);
    Task<bool> DeletePeriodAsync(long planId, long itemId, long periodId);

    // === 互斥配置管理 ===
    Task<List<CostPlanExclusionDto>> GetExclusionsAsync(long planId);
    Task<CostPlanExclusionDto> CreateExclusionAsync(long planId, CreateExclusionRequest request);
    Task<CostPlanExclusionDto?> UpdateExclusionAsync(long planId, long exclusionId, UpdateExclusionRequest request);
    Task<bool> DeleteExclusionAsync(long planId, long exclusionId);

    // === 运单成本计算 ===
    Task<EffectiveCostResult?> GetEffectiveCostAsync(string brandCode, long outletId, string? shopName, DateTime businessDate);

    // === 矩阵保存/读取 ===
    Task SaveItemMatrixAsync(long planId, long itemId, SaveItemMatrixRequest request);
    Task<CostItemMatrixDto?> GetItemMatrixAsync(long planId, long itemId, DateTime? effectiveDate = null);

    // === 城市查询 ===
    Task<List<CityDto>> GetCitiesAsync(string? keyword);
}
