using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public class NotificationDispatcher : INotificationDispatcher
{
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IEnumerable<INotificationChannel> channels,
        STOTOPDbContext dbContext,
        ILogger<NotificationDispatcher> logger)
    {
        _channels = channels;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task DispatchCreateTodoAsync(long todoItemId)
    {
        var todo = await _dbContext.Set<CfTodoItem>().FirstOrDefaultAsync(t => t.FID == todoItemId);
        if (todo == null) return;

        var channelName = todo.FPushChannel;
        if (string.IsNullOrWhiteSpace(channelName))
        {
            todo.FPushStatus = "skipped";
            await _dbContext.SaveChangesAsync();
            return;
        }

        var channel = _channels.FirstOrDefault(c => c.ChannelName.Equals(channelName, StringComparison.OrdinalIgnoreCase));
        if (channel == null)
        {
            _logger.LogWarning("推送渠道 {Channel} 未注册，TodoItemId={Id}", channelName, todoItemId);
            todo.FPushStatus = "failed";
            await _dbContext.SaveChangesAsync();
            return;
        }

        try
        {
            var payload = new NotificationPayload(
                TodoItemId: todo.FID,
                Subject: todo.FTitle ?? "新待办",
                DetailUrl: $"/cardflow/todo/{todo.FCardId}",
                RecipientExternalId: todo.FHandlerId.ToString(),
                Extra: new Dictionary<string, object>
                {
                    ["orgId"] = todo.FOrgId,
                    ["handlerId"] = todo.FHandlerId
                });

            var externalId = await channel.CreateTodoAsync(payload);
            todo.FExternalTodoId = externalId;
            todo.FPushStatus = "success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送待办失败，TodoItemId={Id}, Channel={Channel}", todoItemId, channelName);
            todo.FPushStatus = "failed";
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task DispatchCompleteTodoAsync(long todoItemId)
    {
        var todo = await _dbContext.Set<CfTodoItem>().FirstOrDefaultAsync(t => t.FID == todoItemId);
        if (todo == null || string.IsNullOrWhiteSpace(todo.FExternalTodoId)) return;

        var channel = _channels.FirstOrDefault(c => c.ChannelName.Equals(todo.FPushChannel, StringComparison.OrdinalIgnoreCase));
        if (channel == null) return;

        try
        {
            await channel.CompleteTodoAsync(todo.FExternalTodoId);
            todo.FPushStatus = "completed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "完成外部待办失败，TodoItemId={Id}", todoItemId);
            todo.FPushStatus = "failed";
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task DispatchDeleteTodoAsync(long todoItemId)
    {
        var todo = await _dbContext.Set<CfTodoItem>().FirstOrDefaultAsync(t => t.FID == todoItemId);
        if (todo == null || string.IsNullOrWhiteSpace(todo.FExternalTodoId)) return;

        var channel = _channels.FirstOrDefault(c => c.ChannelName.Equals(todo.FPushChannel, StringComparison.OrdinalIgnoreCase));
        if (channel == null) return;

        try
        {
            await channel.DeleteTodoAsync(todo.FExternalTodoId);
            todo.FPushStatus = "deleted";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除外部待办失败，TodoItemId={Id}", todoItemId);
            todo.FPushStatus = "failed";
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task RetryPushAsync(long todoItemId)
    {
        var todo = await _dbContext.Set<CfTodoItem>().FirstOrDefaultAsync(t => t.FID == todoItemId);
        if (todo == null || todo.FPushStatus != "failed") return;

        // 重新尝试推送
        await DispatchCreateTodoAsync(todoItemId);
    }
}
