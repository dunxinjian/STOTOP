using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface ITodoService
{
    Task<PagedResult<TodoItemDto>> GetMyTodosAsync(long userId, TodoQueryRequest request);
    Task<PagedResult<TodoItemDto>> GetMyCcAsync(long userId, TodoQueryRequest request);
    Task<TodoCountDto> GetCountAsync(long userId);
    Task<TodoStatsDto> GetStatsAsync(long orgId);
    Task<TodoStatsDto> GetStatsAsync(TodoStatsRequest request);
    Task<long> CreateTodoAsync(long cardId, long stageInstanceId, long handlerId, string handlerName, string title, string type = "todo", string? pushChannel = null);
    Task CompleteTodoAsync(long todoItemId);
    Task CancelTodoAsync(long todoItemId);
}
