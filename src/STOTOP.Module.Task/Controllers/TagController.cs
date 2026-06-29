using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Task.Controllers;

[Authorize]
[ApiController]
[Route("api/task/tags")]
public class TagController : ControllerBase
{
    private readonly ITagService _service;

    public TagController(ITagService service)
    {
        _service = service;
    }

    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>获取标签列表（当前组织）</summary>
    [HttpGet]
    [RequirePermission(TaskPermissions.TagManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<TagListDto>>> GetList()
    {
        return await _service.GetListAsync(GetOrgId());
    }

    /// <summary>创建标签</summary>
    [HttpPost]
    [RequirePermission(TaskPermissions.TagManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<TagListDto>> Create([FromBody] CreateTagRequest request)
    {
        return await _service.CreateAsync(request, GetOrgId());
    }

    /// <summary>更新标签</summary>
    [HttpPut("{id}")]
    [RequirePermission(TaskPermissions.TagManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<TagListDto>> Update(long id, [FromBody] UpdateTagRequest request)
    {
        return await _service.UpdateAsync(id, request);
    }

    /// <summary>删除标签</summary>
    [HttpDelete("{id}")]
    [RequirePermission(TaskPermissions.TagManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(id);
    }
}
