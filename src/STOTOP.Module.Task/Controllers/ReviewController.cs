using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Task.Controllers;

[Authorize]
[ApiController]
[Route("api/task/reviews")]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _service;

    public ReviewController(IReviewService service)
    {
        _service = service;
    }

    /// <summary>复盘记录列表（分页，支持按关联类型/复盘人筛选）</summary>
    [HttpGet]
    [RequirePermission(TaskPermissions.ReviewView)]
    public async global::System.Threading.Tasks.Task<ApiResult<PagedResult<ReviewListDto>>> GetPagedList([FromQuery] ReviewPagedRequest query)
    {
        return await _service.GetPagedListAsync(query);
    }

    /// <summary>复盘详情</summary>
    [HttpGet("{id}")]
    [RequirePermission(TaskPermissions.ReviewView)]
    public async global::System.Threading.Tasks.Task<ApiResult<ReviewDetailDto>> GetById(long id)
    {
        return await _service.GetByIdAsync(id);
    }

    /// <summary>创建复盘记录（关联任务/项目/目标/KR）</summary>
    [HttpPost]
    [RequirePermission(TaskPermissions.ReviewCreate)]
    public async global::System.Threading.Tasks.Task<ApiResult<ReviewDetailDto>> Create([FromBody] CreateReviewRequest request)
    {
        return await _service.CreateAsync(request);
    }

    /// <summary>更新复盘记录</summary>
    [HttpPut("{id}")]
    [RequirePermission(TaskPermissions.ReviewEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<ReviewDetailDto>> Update(long id, [FromBody] UpdateReviewRequest request)
    {
        return await _service.UpdateAsync(id, request);
    }

    /// <summary>发布复盘</summary>
    [HttpPut("{id}/publish")]
    [RequirePermission(TaskPermissions.ReviewEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> Publish(long id)
    {
        return await _service.PublishAsync(id);
    }

    /// <summary>删除复盘（仅草稿可删）</summary>
    [HttpDelete("{id}")]
    [RequirePermission(TaskPermissions.ReviewDelete)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(id);
    }

    /// <summary>从复盘提炼为知识库文章（一键沉淀）</summary>
    [HttpPost("{id}/extract-knowledge")]
    [RequirePermission(TaskPermissions.KnowledgeCreate)]
    public async global::System.Threading.Tasks.Task<ApiResult<KnowledgeDetailDto>> ExtractKnowledge(long id, [FromBody] ExtractKnowledgeRequest request)
    {
        return await _service.ExtractKnowledgeAsync(id, request);
    }

    /// <summary>获取指定任务/项目/目标的复盘列表</summary>
    [HttpGet("~/api/task/{type}/{entityId}/reviews")]
    [RequirePermission(TaskPermissions.ReviewView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<ReviewListDto>>> GetByEntity(int type, long entityId)
    {
        return await _service.GetByEntityAsync(type, entityId);
    }
}
