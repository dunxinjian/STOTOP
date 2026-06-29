using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Task.Controllers;

[Authorize]
[ApiController]
[Route("api/task/knowledge")]
public class KnowledgeController : ControllerBase
{
    private readonly IKnowledgeService _service;

    public KnowledgeController(IKnowledgeService service)
    {
        _service = service;
    }

    /// <summary>知识库列表（分页，支持分类/标签/关键词搜索）</summary>
    [HttpGet]
    [RequirePermission(TaskPermissions.KnowledgeView)]
    public async global::System.Threading.Tasks.Task<ApiResult<PagedResult<KnowledgeListDto>>> GetPagedList([FromQuery] KnowledgePagedRequest query)
    {
        return await _service.GetPagedListAsync(query);
    }

    /// <summary>知识详情（自动+1浏览数）</summary>
    [HttpGet("{id:long}")]
    [RequirePermission(TaskPermissions.KnowledgeView)]
    public async global::System.Threading.Tasks.Task<ApiResult<KnowledgeDetailDto>> GetById(long id)
    {
        return await _service.GetByIdAsync(id);
    }

    /// <summary>创建知识文章</summary>
    [HttpPost]
    [RequirePermission(TaskPermissions.KnowledgeCreate)]
    public async global::System.Threading.Tasks.Task<ApiResult<KnowledgeDetailDto>> Create([FromBody] CreateKnowledgeRequest request)
    {
        return await _service.CreateAsync(request);
    }

    /// <summary>更新知识文章</summary>
    [HttpPut("{id:long}")]
    [RequirePermission(TaskPermissions.KnowledgeEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<KnowledgeDetailDto>> Update(long id, [FromBody] UpdateKnowledgeRequest request)
    {
        return await _service.UpdateAsync(id, request);
    }

    /// <summary>删除知识文章</summary>
    [HttpDelete("{id:long}")]
    [RequirePermission(TaskPermissions.KnowledgeDelete)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(id);
    }

    /// <summary>点赞/取消点赞</summary>
    [HttpPost("{id:long}/like")]
    [RequirePermission(TaskPermissions.KnowledgeView)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> ToggleLike(long id)
    {
        return await _service.ToggleLikeAsync(id);
    }

    /// <summary>收藏/取消收藏</summary>
    [HttpPost("{id:long}/collect")]
    [RequirePermission(TaskPermissions.KnowledgeView)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> ToggleCollect(long id)
    {
        return await _service.ToggleCollectAsync(id);
    }

    /// <summary>获取知识评论列表</summary>
    [HttpGet("{id:long}/comments")]
    [RequirePermission(TaskPermissions.KnowledgeView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<KnowledgeCommentDto>>> GetComments(long id)
    {
        return await _service.GetCommentsAsync(id);
    }

    /// <summary>添加知识评论</summary>
    [HttpPost("{id:long}/comments")]
    [RequirePermission(TaskPermissions.KnowledgeView)]
    public async global::System.Threading.Tasks.Task<ApiResult<KnowledgeCommentDto>> CreateComment(long id, [FromBody] CreateKnowledgeCommentRequest request)
    {
        return await _service.CreateCommentAsync(id, request);
    }

    /// <summary>我的收藏</summary>
    [HttpGet("my-collections")]
    [RequirePermission(TaskPermissions.KnowledgeView)]
    public async global::System.Threading.Tasks.Task<ApiResult<PagedResult<KnowledgeListDto>>> GetMyCollections()
    {
        return await _service.GetMyCollectionsAsync();
    }

    /// <summary>热门知识（按浏览/点赞排序）</summary>
    [HttpGet("hot")]
    [RequirePermission(TaskPermissions.KnowledgeView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<KnowledgeListDto>>> GetHot()
    {
        return await _service.GetHotAsync();
    }
}
