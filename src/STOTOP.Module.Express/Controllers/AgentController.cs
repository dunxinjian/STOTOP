using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 业务代理管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/agents")]
public class AgentController : ControllerBase
{
    private readonly IAgentService _service;

    public AgentController(IAgentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<AgentDto>>> GetList([FromQuery] AgentQueryRequest request)
    {
        var result = await _service.GetListAsync(request);
        return ApiResult<PagedResult<AgentDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<AgentDto>> GetById(string id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return ApiResult<AgentDto>.Fail("业务代理不存在");
        return ApiResult<AgentDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<AgentDto>> Create([FromBody] CreateAgentRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return ApiResult<AgentDto>.Success(result, "创建业务代理成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AgentDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<AgentDto>> Update(string id, [FromBody] UpdateAgentRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request);
            if (result == null)
                return ApiResult<AgentDto>.Fail("业务代理不存在");
            return ApiResult<AgentDto>.Success(result, "更新业务代理成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AgentDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(string id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result)
            return ApiResult.Fail("业务代理不存在");
        return ApiResult.Ok("删除业务代理成功");
    }
}
