using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface IReviewService
{
    Task<ApiResult<PagedResult<ReviewListDto>>> GetPagedListAsync(ReviewPagedRequest request);
    Task<ApiResult<ReviewDetailDto>> GetByIdAsync(long id);
    Task<ApiResult<ReviewDetailDto>> CreateAsync(CreateReviewRequest request);
    Task<ApiResult<ReviewDetailDto>> UpdateAsync(long id, UpdateReviewRequest request);
    Task<ApiResult<bool>> PublishAsync(long id);
    Task<ApiResult<bool>> DeleteAsync(long id);
    Task<ApiResult<KnowledgeDetailDto>> ExtractKnowledgeAsync(long id, ExtractKnowledgeRequest request);
    Task<ApiResult<List<ReviewListDto>>> GetByEntityAsync(int relationType, long entityId);
}
