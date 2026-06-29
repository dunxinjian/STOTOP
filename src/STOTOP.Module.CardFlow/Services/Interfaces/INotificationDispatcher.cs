namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface INotificationDispatcher
{
    Task DispatchCreateTodoAsync(long todoItemId);
    Task DispatchCompleteTodoAsync(long todoItemId);
    Task DispatchDeleteTodoAsync(long todoItemId);
    Task RetryPushAsync(long todoItemId);
}
