using STOTOP.Core.Models;
using STOTOP.Module.Quality.Dtos;

namespace STOTOP.Module.Quality.Services.Rule;

public interface IQualityRuleService
{
    /// <summary>规则分页列表</summary>
    Task<ApiResult<PagedResult<QualityRuleDto>>> GetPagedAsync(long orgId, RulePagedRequest request);
    /// <summary>规则详情</summary>
    Task<ApiResult<QualityRuleDetailDto>> GetByIdAsync(long orgId, long id);
    /// <summary>创建规则</summary>
    Task<ApiResult<QualityRuleDto>> CreateAsync(long orgId, long userId, CreateRuleRequest request);
    /// <summary>更新规则</summary>
    Task<ApiResult<QualityRuleDto>> UpdateAsync(long orgId, long userId, long id, UpdateRuleRequest request);
    /// <summary>删除规则</summary>
    Task<ApiResult<bool>> DeleteAsync(long orgId, long id);
    /// <summary>启用/禁用规则</summary>
    Task<ApiResult<bool>> ToggleAsync(long orgId, long id);
}
