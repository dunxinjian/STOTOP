using STOTOP.Core.Models;
using STOTOP.Module.Quality.Dtos;

namespace STOTOP.Module.Quality.Services.Knowledge;

public interface IKnowledgeService
{
    /// <summary>知识库分页列表</summary>
    Task<ApiResult<PagedResult<KnowledgeDto>>> GetPagedAsync(long orgId, KnowledgePagedRequest request);
    /// <summary>知识库详情</summary>
    Task<ApiResult<KnowledgeDto>> GetByIdAsync(long orgId, long id);
    /// <summary>创建知识库文章</summary>
    Task<ApiResult<KnowledgeDto>> CreateAsync(long orgId, long operatorId, CreateKnowledgeRequest request);
    /// <summary>更新知识库文章</summary>
    Task<ApiResult<KnowledgeDto>> UpdateAsync(long orgId, long operatorId, long id, UpdateKnowledgeRequest request);
    /// <summary>删除知识库文章</summary>
    Task<ApiResult<bool>> DeleteAsync(long orgId, long id);
    /// <summary>获取所有分类</summary>
    Task<ApiResult<List<string>>> GetCategoriesAsync(long orgId);
    /// <summary>获取所有标签</summary>
    Task<ApiResult<List<string>>> GetTagsAsync(long orgId);
    /// <summary>知识库统计</summary>
    Task<ApiResult<KnowledgeStatsDto>> GetStatsAsync(long orgId);
}
