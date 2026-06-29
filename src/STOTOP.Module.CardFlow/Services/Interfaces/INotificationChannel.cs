namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface INotificationChannel
{
    string ChannelName { get; }
    Task<string?> CreateTodoAsync(NotificationPayload payload);
    Task CompleteTodoAsync(string externalTodoId);
    Task DeleteTodoAsync(string externalTodoId);
    Task<bool> ValidateCallbackAsync(CallbackContext context);
    Task HandleCallbackAsync(CallbackContext context);
}

public record NotificationPayload(
    long TodoItemId, string Subject, string DetailUrl,
    string RecipientExternalId, Dictionary<string, object>? Extra);

public record CallbackContext(
    string Channel, string EventType, string RawBody,
    IDictionary<string, string> Headers);
