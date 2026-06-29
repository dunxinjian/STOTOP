using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/flow-groups")]
public class FlowGroupController : ControllerBase
{
    private readonly IFlowGroupService _service;

    public FlowGroupController(IFlowGroupService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    [HttpGet]
    public async Task<ApiResult<List<FlowGroupDto>>> GetList([FromQuery] long orgId)
    {
        var result = await _service.GetListAsync(orgId);
        return ApiResult<List<FlowGroupDto>>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<FlowGroupDto>> Create([FromBody] CreateFlowGroupRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request, GetUserId());
            return ApiResult<FlowGroupDto>.Success(result, "创建流程组成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<FlowGroupDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<FlowGroupDto>> Update(long id, [FromBody] UpdateFlowGroupRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request, GetUserId());
            return ApiResult<FlowGroupDto>.Success(result, "更新流程组成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<FlowGroupDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        try
        {
            await _service.DeleteAsync(id, GetUserId());
            return ApiResult.Ok("删除流程组成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpGet("{id}/links")]
    public async Task<ApiResult<List<FlowGroupLinkDto>>> GetLinks(long id)
    {
        var result = await _service.GetLinksAsync(id);
        return ApiResult<List<FlowGroupLinkDto>>.Success(result);
    }

    [HttpPut("{id}/links")]
    public async Task<ApiResult> SaveLinks(long id, [FromBody] List<SaveFlowGroupLinkRequest> links)
    {
        try
        {
            await _service.SaveLinksAsync(id, links, GetUserId());
            return ApiResult.Ok("保存连接成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
