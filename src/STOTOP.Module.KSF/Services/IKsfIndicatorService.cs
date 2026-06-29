using STOTOP.Core.Models;
using STOTOP.Module.KSF.Dtos;

namespace STOTOP.Module.KSF.Services;

/// <summary>
/// KSF 指标服务
/// </summary>
public interface IKsfIndicatorService
{
    Task<ApiResult<List<KsfIndicatorDto>>> GetListAsync(long orgId, bool? enabled = null);
    Task<ApiResult<KsfIndicatorDto>> CreateAsync(long orgId, KsfIndicatorCreateRequest request);
    Task<ApiResult<KsfIndicatorDto>> UpdateAsync(long orgId, long id, KsfIndicatorCreateRequest request);
    Task<ApiResult> DeleteAsync(long orgId, long id);
    Task<ApiResult<bool>> ValidateSqlTemplateAsync(string sqlTemplate);
}
