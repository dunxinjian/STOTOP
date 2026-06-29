using STOTOP.Core.Models;
using STOTOP.Module.Quality.Dtos;

namespace STOTOP.Module.Quality.Services.Review;

public interface IReviewService
{
    /// <summary>复盘分页列表</summary>
    Task<ApiResult<PagedResult<ReviewDto>>> GetPagedAsync(long orgId, ReviewPagedRequest request);
    /// <summary>复盘详情</summary>
    Task<ApiResult<ReviewDto>> GetByIdAsync(long orgId, long id);
    /// <summary>创建复盘</summary>
    Task<ApiResult<ReviewDto>> CreateAsync(long orgId, long operatorId, CreateReviewRequest request);
    /// <summary>更新复盘</summary>
    Task<ApiResult<ReviewDto>> UpdateAsync(long orgId, long operatorId, long id, UpdateReviewRequest request);
    /// <summary>删除复盘</summary>
    Task<ApiResult<bool>> DeleteAsync(long orgId, long id);
    /// <summary>复盘统计</summary>
    Task<ApiResult<ReviewStatsDto>> GetStatsAsync(long orgId);
    /// <summary>改进措施分页列表</summary>
    Task<ApiResult<PagedResult<ImprovementListDto>>> GetImprovementsAsync(long orgId, ImprovementPagedRequest request);
    /// <summary>更新改进措施</summary>
    Task<ApiResult<bool>> UpdateImprovementAsync(long orgId, long id, UpdateImprovementRequest request);
    /// <summary>完成改进措施</summary>
    Task<ApiResult<bool>> CompleteImprovementAsync(long orgId, long id, CompleteImprovementRequest request);
}
