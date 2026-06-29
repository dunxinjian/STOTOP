using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Controllers;

[Authorize]
[ApiController]
[Route("api/dormitory/visitors")]
public class VisitorController : ControllerBase
{
    private readonly IVisitorService _visitorService;

    public VisitorController(IVisitorService visitorService)
    {
        _visitorService = visitorService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<VisitorListItemDto>>> GetList([FromQuery] VisitorQueryRequest request)
    {
        var result = await _visitorService.GetVisitorsAsync(request);
        return ApiResult<PagedResult<VisitorListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<VisitorDto>> GetById(long id)
    {
        var result = await _visitorService.GetVisitorByIdAsync(id);
        if (result == null)
        {
            return ApiResult<VisitorDto>.Fail("访客记录不存在");
        }
        return ApiResult<VisitorDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<VisitorDto>> Create([FromBody] CreateVisitorRequest request)
    {
        try
        {
            var result = await _visitorService.CreateVisitorAsync(request);
            return ApiResult<VisitorDto>.Success(result, "创建访客记录成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VisitorDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<VisitorDto>> Update(long id, [FromBody] UpdateVisitorRequest request)
    {
        var result = await _visitorService.UpdateVisitorAsync(id, request);
        if (result == null)
        {
            return ApiResult<VisitorDto>.Fail("访客记录不存在");
        }
        return ApiResult<VisitorDto>.Success(result, "更新访客记录成功");
    }

    [HttpPut("{id}/leave")]
    public async Task<ApiResult<VisitorDto>> Departure(long id)
    {
        var result = await _visitorService.DepartureAsync(id);
        if (result == null)
        {
            return ApiResult<VisitorDto>.Fail("访客记录不存在");
        }
        return ApiResult<VisitorDto>.Success(result, "登记离开成功");
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _visitorService.DeleteVisitorAsync(id);
        if (!result)
        {
            return ApiResult.Fail("访客记录不存在");
        }
        return ApiResult.Ok("删除访客记录成功");
    }
}
