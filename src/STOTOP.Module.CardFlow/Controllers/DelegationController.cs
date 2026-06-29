using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/delegations")]
public class DelegationController : ControllerBase
{
    private readonly IDelegationService _service;

    public DelegationController(IDelegationService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    [HttpGet]
    public async Task<ApiResult<List<DelegationDto>>> GetMyDelegations()
    {
        var result = await _service.GetMyDelegationsAsync(GetUserId());
        return ApiResult<List<DelegationDto>>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<DelegationDto>> Create([FromBody] CreateDelegationRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request, GetUserId());
            return ApiResult<DelegationDto>.Success(result, "创建委托成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<DelegationDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<DelegationDto>> Update(long id, [FromBody] UpdateDelegationRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request, GetUserId());
            return ApiResult<DelegationDto>.Success(result, "修改委托成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<DelegationDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Cancel(long id)
    {
        try
        {
            await _service.CancelAsync(id, GetUserId());
            return ApiResult.Ok("取消委托成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
