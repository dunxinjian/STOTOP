using STOTOP.Core.Models;
using STOTOP.Module.Salary.Dtos;

namespace STOTOP.Module.Salary.Services;

public interface IPromotionReviewService
{
    Task<ApiResult<List<PromotionReviewDto>>> GetListAsync(long orgId);
    Task<ApiResult<PromotionReviewDto>> CreateAsync(long orgId, CreatePromotionReviewRequest request);
    Task<ApiResult> ApproveAsync(long orgId, long id, long reviewerId, ReviewPromotionRequest request);
    Task<ApiResult> RejectAsync(long orgId, long id, long reviewerId, ReviewPromotionRequest request);
    Task<ApiResult<List<PromotionReviewDto>>> GetPendingListAsync(long orgId);
}
