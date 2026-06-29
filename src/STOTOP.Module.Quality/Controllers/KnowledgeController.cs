using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Services.Knowledge;

namespace STOTOP.Module.Quality.Controllers;

[Authorize]
[ApiController]
[Route("api/quality")]
public class KnowledgeController : ControllerBase
{
    private readonly IKnowledgeService _service;

    public KnowledgeController(IKnowledgeService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>知识库分页列表</summary>
    [HttpGet("knowledge")]
    [RequirePermission(QualityPermissions.KnowledgeView)]
    public async Task<ApiResult<PagedResult<KnowledgeDto>>> GetPaged([FromQuery] KnowledgePagedRequest request)
    {
        return await _service.GetPagedAsync(GetOrgId(), request);
    }

    /// <summary>知识库详情</summary>
    [HttpGet("knowledge/{id}")]
    [RequirePermission(QualityPermissions.KnowledgeView)]
    public async Task<ApiResult<KnowledgeDto>> GetById(long id)
    {
        return await _service.GetByIdAsync(GetOrgId(), id);
    }

    /// <summary>创建知识库文章</summary>
    [HttpPost("knowledge")]
    [RequirePermission(QualityPermissions.KnowledgeManage)]
    public async Task<ApiResult<KnowledgeDto>> Create([FromBody] CreateKnowledgeRequest request)
    {
        return await _service.CreateAsync(GetOrgId(), GetUserId(), request);
    }

    /// <summary>更新知识库文章</summary>
    [HttpPut("knowledge/{id}")]
    [RequirePermission(QualityPermissions.KnowledgeManage)]
    public async Task<ApiResult<KnowledgeDto>> Update(long id, [FromBody] UpdateKnowledgeRequest request)
    {
        return await _service.UpdateAsync(GetOrgId(), GetUserId(), id, request);
    }

    /// <summary>删除知识库文章</summary>
    [HttpDelete("knowledge/{id}")]
    [RequirePermission(QualityPermissions.KnowledgeManage)]
    public async Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(GetOrgId(), id);
    }

    /// <summary>获取知识库分类列表</summary>
    [HttpGet("knowledge/categories")]
    [RequirePermission(QualityPermissions.KnowledgeView)]
    public async Task<ApiResult<List<string>>> GetCategories()
    {
        return await _service.GetCategoriesAsync(GetOrgId());
    }

    /// <summary>获取知识库标签列表</summary>
    [HttpGet("knowledge/tags")]
    [RequirePermission(QualityPermissions.KnowledgeView)]
    public async Task<ApiResult<List<string>>> GetTags()
    {
        return await _service.GetTagsAsync(GetOrgId());
    }

    /// <summary>知识库统计</summary>
    [HttpGet("knowledge/stats")]
    [RequirePermission(QualityPermissions.KnowledgeView)]
    public async Task<ApiResult<KnowledgeStatsDto>> GetStats()
    {
        return await _service.GetStatsAsync(GetOrgId());
    }
}
