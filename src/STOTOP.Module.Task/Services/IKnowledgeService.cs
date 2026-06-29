using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface IKnowledgeService
{
    Task<ApiResult<PagedResult<KnowledgeListDto>>> GetPagedListAsync(KnowledgePagedRequest request);
    Task<ApiResult<KnowledgeDetailDto>> GetByIdAsync(long id);
    Task<ApiResult<KnowledgeDetailDto>> CreateAsync(CreateKnowledgeRequest request);
    Task<ApiResult<KnowledgeDetailDto>> UpdateAsync(long id, UpdateKnowledgeRequest request);
    Task<ApiResult<bool>> DeleteAsync(long id);
    Task<ApiResult<bool>> ToggleLikeAsync(long id);
    Task<ApiResult<bool>> ToggleCollectAsync(long id);
    Task<ApiResult<List<KnowledgeCommentDto>>> GetCommentsAsync(long knowledgeId);
    Task<ApiResult<KnowledgeCommentDto>> CreateCommentAsync(long knowledgeId, CreateKnowledgeCommentRequest request);
    Task<ApiResult<PagedResult<KnowledgeListDto>>> GetMyCollectionsAsync();
    Task<ApiResult<List<KnowledgeListDto>>> GetHotAsync();
}
