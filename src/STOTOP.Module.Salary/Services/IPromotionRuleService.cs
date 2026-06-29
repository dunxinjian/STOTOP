using STOTOP.Core.Models;
using STOTOP.Module.Salary.Dtos;

namespace STOTOP.Module.Salary.Services;

public interface IPromotionRuleService
{
    Task<ApiResult<List<PromotionRuleDto>>> GetListAsync(long orgId);
    Task<ApiResult<PromotionRuleDto>> CreateAsync(long orgId, CreatePromotionRuleRequest request);
    Task<ApiResult<PromotionRuleDto>> UpdateAsync(long orgId, long id, UpdatePromotionRuleRequest request);
    Task<ApiResult> EnableAsync(long orgId, long id);
}
