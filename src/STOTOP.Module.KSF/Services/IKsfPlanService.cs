using STOTOP.Core.Models;
using STOTOP.Module.KSF.Dtos;

namespace STOTOP.Module.KSF.Services;

/// <summary>
/// KSF 岗位方案服务
/// </summary>
public interface IKsfPlanService
{
    Task<ApiResult<List<KsfPlanDto>>> GetListAsync(long orgId, int? runMode = null);
    Task<ApiResult<KsfPlanDto>> CreateAsync(long orgId, KsfPlanCreateRequest request);
    Task<ApiResult<KsfPlanDto>> UpdateAsync(long orgId, long id, KsfPlanCreateRequest request);
    Task<ApiResult> ActivateAsync(long orgId, long planId, DateTime effectiveFrom);
    Task<ApiResult<KsfPlanDto?>> GetByPositionAsync(long orgId, long positionId);
}
