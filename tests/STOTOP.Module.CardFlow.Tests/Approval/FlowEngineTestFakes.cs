using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Tests.Approval;

internal sealed class FakeNumberSequenceService : INumberSequenceService
{
    public global::System.Threading.Tasks.Task<string> GenerateNumberAsync(long flowDefinitionId, long orgId, string template)
        => global::System.Threading.Tasks.Task.FromResult("TEST-001");
}

internal sealed class FakeCardSchemaService : ICardSchemaService
{
    public ValidationResult ValidateCardData(string schemaJson, string dataJson) => new(true, new List<string>());
    public string GenerateTitle(string template, string dataJson, string flowName, string cardNumber) => flowName;
}

internal sealed class DbTodoService : ITodoService
{
    private readonly STOTOP.Infrastructure.Data.STOTOPDbContext _db;

    public DbTodoService(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        _db = db;
    }

    public global::System.Threading.Tasks.Task<PagedResult<TodoItemDto>> GetMyTodosAsync(long userId, TodoQueryRequest request) => throw new NotSupportedException();
    public global::System.Threading.Tasks.Task<PagedResult<TodoItemDto>> GetMyCcAsync(long userId, TodoQueryRequest request) => throw new NotSupportedException();
    public global::System.Threading.Tasks.Task<TodoCountDto> GetCountAsync(long userId) => throw new NotSupportedException();
    public global::System.Threading.Tasks.Task<TodoStatsDto> GetStatsAsync(long orgId) => throw new NotSupportedException();
    public global::System.Threading.Tasks.Task<TodoStatsDto> GetStatsAsync(TodoStatsRequest request) => throw new NotSupportedException();

    public async global::System.Threading.Tasks.Task<long> CreateTodoAsync(long cardId, long stageInstanceId, long handlerId, string handlerName, string title, string type = "todo", string? pushChannel = null)
    {
        var todo = new CfTodoItem
        {
            FCardId = cardId,
            FStageInstanceId = stageInstanceId,
            FHandlerId = handlerId,
            FHandlerName = handlerName,
            FTitle = title,
            FType = type,
            FStatus = "pending",
            FOrgId = 1
        };
        _db.Set<CfTodoItem>().Add(todo);
        await _db.SaveChangesAsync();
        return todo.FID;
    }

    public global::System.Threading.Tasks.Task CompleteTodoAsync(long todoItemId) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task CancelTodoAsync(long todoItemId) => global::System.Threading.Tasks.Task.CompletedTask;
}

internal sealed class FakeNotificationDispatcher : INotificationDispatcher
{
    public global::System.Threading.Tasks.Task DispatchCreateTodoAsync(long todoItemId) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task DispatchCompleteTodoAsync(long todoItemId) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task DispatchDeleteTodoAsync(long todoItemId) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task RetryPushAsync(long todoItemId) => global::System.Threading.Tasks.Task.CompletedTask;
}

internal sealed class FakeBudgetOccupationService : IBudgetOccupationService
{
    public global::System.Threading.Tasks.Task<BudgetPreviewResult> PreviewAsync(BudgetPreviewRequest request)
        => global::System.Threading.Tasks.Task.FromResult(new BudgetPreviewResult());

    public global::System.Threading.Tasks.Task OccupyAsync(BudgetPreviewRequest request, string transitionKey) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task LockAsync(string sourceType, long sourceId, string transitionKey) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task ConsumeAsync(string sourceType, long sourceId, decimal amount, string transitionKey) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task ReleaseAsync(string sourceType, long sourceId, string transitionKey) => global::System.Threading.Tasks.Task.CompletedTask;
}

internal sealed class FakeBatchNotifier : IBatchNotifier
{
    public global::System.Threading.Tasks.Task PipelineStartedAsync(long batchId, IEnumerable<PluginSnapshot> plugins) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task PluginStatusChangedAsync(long batchId, int pluginIndex, string pluginName, string status, string? error = null) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task ProgressUpdateAsync(long batchId, int processed, int total) => global::System.Threading.Tasks.Task.CompletedTask;
}

internal sealed class FakeBatchLifecycleService : IBatchLifecycleService
{
    public global::System.Threading.Tasks.Task RefreshBatchStatusAsync(long batchId) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task RevokeBatchAsync(long batchId, long operatorId) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task<BatchProgressDto> GetBatchProgressAsync(long batchId)
        => global::System.Threading.Tasks.Task.FromResult(new BatchProgressDto());

    public global::System.Threading.Tasks.Task TransitionBatchStatusAsync(CfBatch batch, int newStatus, string? message = null) => global::System.Threading.Tasks.Task.CompletedTask;
}
