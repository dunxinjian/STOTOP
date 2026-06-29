using STOTOP.Core.Models;
using STOTOP.Module.Points.Dtos;

namespace STOTOP.Module.Points.Services;

public interface IPointRuleService
{
    Task<ApiResult<PagedResult<PointRuleListDto>>> GetPagedListAsync(long orgId, PointRulePagedRequest request);
    Task<ApiResult<PointRuleDetailDto>> GetByIdAsync(long id);
    Task<ApiResult<PointRuleDetailDto>> CreateAsync(long orgId, CreatePointRuleRequest request);
    Task<ApiResult<PointRuleDetailDto>> UpdateAsync(long id, UpdatePointRuleRequest request);
    Task<ApiResult<bool>> DeleteAsync(long id);
    Task<ApiResult<bool>> ToggleAsync(long id);
    /// <summary>
    /// 内部方法：根据事件类型匹配启用的规则
    /// </summary>
    Task<List<PointRuleDetailDto>> MatchRulesAsync(long orgId, string eventType, object? context);
}
