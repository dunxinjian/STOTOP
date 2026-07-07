using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/definitions")]
public class FlowDefinitionController : ControllerBase
{
    private readonly IFlowDefinitionService _service;
    private readonly ICardFlowPathPreviewService _pathPreviewService;

    public FlowDefinitionController(
        IFlowDefinitionService service,
        ICardFlowPathPreviewService pathPreviewService)
    {
        _service = service;
        _pathPreviewService = pathPreviewService;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    [HttpGet]
    public async Task<ApiResult<PagedResult<FlowDefinitionDto>>> GetList([FromQuery] FlowDefinitionQueryRequest request)
    {
        var result = await _service.GetListAsync(request);
        return ApiResult<PagedResult<FlowDefinitionDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<FlowDefinitionDto>> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return ApiResult<FlowDefinitionDto>.Fail("流程定义不存在");
        return ApiResult<FlowDefinitionDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<FlowDefinitionDto>> Create([FromBody] CreateFlowDefinitionRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request, GetUserId());
            return ApiResult<FlowDefinitionDto>.Success(result, "创建流程定义成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<FlowDefinitionDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<FlowDefinitionDto>> Update(long id, [FromBody] UpdateFlowDefinitionRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request, GetUserId());
            return ApiResult<FlowDefinitionDto>.Success(result, "更新流程定义成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<FlowDefinitionDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        try
        {
            await _service.DeleteAsync(id, GetUserId());
            return ApiResult.Ok("删除成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/publish")]
    public async Task<ApiResult> Publish(long id)
    {
        try
        {
            await _service.PublishAsync(id, GetUserId());
            return ApiResult.Ok("发布成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/archive")]
    public async Task<ApiResult> Archive(long id)
    {
        try
        {
            await _service.ArchiveAsync(id, GetUserId());
            return ApiResult.Ok("归档成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/disable")]
    public async Task<ApiResult> Disable(long id)
    {
        try
        {
            await _service.DisableAsync(id, GetUserId());
            return ApiResult.Ok("停用成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return ApiResult.Fail("停用失败：" + ex.Message);
        }
    }

    [HttpPost("{id}/enable")]
    public async Task<ApiResult> Enable(long id)
    {
        try
        {
            await _service.EnableAsync(id, GetUserId());
            return ApiResult.Ok("启用成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return ApiResult.Fail("启用失败：" + ex.Message);
        }
    }

    [HttpGet("{id}/versions")]
    public async Task<ApiResult<List<FlowVersionDto>>> GetVersions(long id)
    {
        var result = await _service.GetVersionsAsync(id);
        return ApiResult<List<FlowVersionDto>>.Success(result);
    }

    /// <summary>
    /// 在途卡片计数（按版本分组，含被卡节点 stageKey）
    /// </summary>
    [HttpGet("{id}/inflight-summary")]
    public async Task<ApiResult<InflightSummaryDto>> GetInflightSummary(long id)
    {
        var result = await _service.GetInflightSummaryAsync(id);
        return ApiResult<InflightSummaryDto>.Success(result);
    }

    [HttpGet("{id}/versions/{versionId}")]
    public async Task<ApiResult<FlowVersionDetailDto>> GetVersionDetail(long id, long versionId)
    {
        var result = await _service.GetVersionDetailAsync(id, versionId);
        if (result == null)
            return ApiResult<FlowVersionDetailDto>.Fail("版本不存在");
        return ApiResult<FlowVersionDetailDto>.Success(result);
    }

    /// <summary>
    /// 回滚：以指定历史版本快照创建新草稿（已存在草稿时拒绝）
    /// </summary>
    [HttpPost("{id}/versions/{versionId}/create-draft")]
    public async Task<ApiResult<FlowVersionDetailDto>> CreateDraftFromVersion(long id, long versionId)
    {
        try
        {
            var result = await _service.CreateDraftFromVersionAsync(id, versionId, GetUserId());
            return ApiResult<FlowVersionDetailDto>.Success(result, "已从历史版本创建草稿");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<FlowVersionDetailDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}/draft-version")]
    public async Task<ApiResult<FlowVersionDetailDto>> SaveDraftVersion(long id, [FromBody] SaveDraftVersionRequest request)
    {
        try
        {
            var result = await _service.SaveDraftVersionAsync(id, request, GetUserId());
            return ApiResult<FlowVersionDetailDto>.Success(result, "草稿保存成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<FlowVersionDetailDto>.Fail(ex.Message);
        }
    }

    [HttpGet("{id}/draft-version")]
    public async Task<ApiResult<FlowVersionDetailDto?>> GetDraftVersion(long id)
    {
        // 无草稿且无已发布版本（新建后从未保存过）属正常场景，返回 Success(null) 避免前端弹错
        var result = await _service.GetDraftVersionAsync(id);
        return ApiResult<FlowVersionDetailDto?>.Success(result);
    }

    [HttpDelete("{id}/draft-version")]
    public async Task<ApiResult> DiscardDraftVersion(long id)
    {
        try
        {
            await _service.DiscardDraftVersionAsync(id, GetUserId());
            return ApiResult.Ok("草稿已放弃");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/draft-version/preview-path")]
    public async Task<ApiResult<CardFlowPathPreviewDto>> PreviewDraftPath(
        long id,
        [FromBody] CardFlowPathPreviewRequest request)
    {
        try
        {
            var result = await _pathPreviewService.PreviewDraftVersionAsync(id, request, HttpContext.RequestAborted);
            return ApiResult<CardFlowPathPreviewDto>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CardFlowPathPreviewDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 克隆流程定义（含版本和节点链）
    /// </summary>
    [HttpPost("{id}/clone")]
    public async Task<ApiResult<FlowDefinitionDto>> Clone(long id, [FromBody] CloneFlowDefinitionRequest request)
    {
        try
        {
            var result = await _service.CloneFlowDefinitionAsync(id, request, GetUserId());
            return ApiResult<FlowDefinitionDto>.Success(result, "克隆流程定义成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<FlowDefinitionDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取流程模板列表（FOrgId=0 且 状态=published）
    /// </summary>
    [HttpGet("/api/cardflow/templates")]
    public async Task<ApiResult<List<FlowDefinitionDto>>> GetTemplates()
    {
        var result = await _service.GetTemplatesAsync();
        return ApiResult<List<FlowDefinitionDto>>.Success(result);
    }

    /// <summary>
    /// 将当前流程保存为模板（复制为 FOrgId=0 的全局模板）
    /// </summary>
    [HttpPost("{id}/save-as-template")]
    public async Task<ApiResult<FlowDefinitionDto>> SaveAsTemplate(long id)
    {
        try
        {
            var result = await _service.SaveAsTemplateAsync(id, GetUserId());
            return ApiResult<FlowDefinitionDto>.Success(result, "已保存为模板");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<FlowDefinitionDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 从模板克隆到当前组织
    /// </summary>
    [HttpPost("/api/cardflow/templates/{id}/clone-to-org")]
    public async Task<ApiResult<FlowDefinitionDto>> CloneTemplateToOrg(long id, [FromBody] CloneFlowDefinitionRequest request)
    {
        try
        {
            var result = await _service.CloneFlowDefinitionAsync(id, request, GetUserId());
            return ApiResult<FlowDefinitionDto>.Success(result, "从模板克隆成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<FlowDefinitionDto>.Fail(ex.Message);
        }
    }
}
