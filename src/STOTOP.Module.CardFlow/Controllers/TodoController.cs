using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/todos")]
public class TodoController : ControllerBase
{
    private readonly ITodoService _service;

    public TodoController(ITodoService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    [HttpGet("mine")]
    public async Task<ApiResult<PagedResult<TodoItemDto>>> GetMyTodos([FromQuery] TodoQueryRequest request)
    {
        var result = await _service.GetMyTodosAsync(GetUserId(), request);
        return ApiResult<PagedResult<TodoItemDto>>.Success(result);
    }

    [HttpGet("cc")]
    public async Task<ApiResult<PagedResult<TodoItemDto>>> GetMyCc([FromQuery] TodoQueryRequest request)
    {
        var result = await _service.GetMyCcAsync(GetUserId(), request);
        return ApiResult<PagedResult<TodoItemDto>>.Success(result);
    }

    [HttpGet("count")]
    public async Task<ApiResult<TodoCountDto>> GetCount()
    {
        var result = await _service.GetCountAsync(GetUserId());
        return ApiResult<TodoCountDto>.Success(result);
    }

    [HttpGet("stats")]
    public async Task<ApiResult<TodoStatsDto>> GetStats([FromQuery] TodoStatsRequest request)
    {
        var result = await _service.GetStatsAsync(request);
        return ApiResult<TodoStatsDto>.Success(result);
    }
}
