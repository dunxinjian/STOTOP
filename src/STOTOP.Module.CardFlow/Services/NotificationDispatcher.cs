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
    private readonly ITodoDispatchLogService _dispatchLog;
    private readonly STOTOP.Core.Services.ITenantTodoChannelResolver _channelResolver;

    public NotificationDispatcher(
        IEnumerable<INotificationChannel> channels,
        STOTOPDbContext dbContext,
        ILogger<NotificationDispatcher> logger,
        ITodoDispatchLogService dispatchLog,
        STOTOP.Core.Services.ITenantTodoChannelResolver channelResolver)
    {
        _channels = channels;
        _dbContext = dbContext;
        _logger = logger;
        _dispatchLog = dispatchLog;
        _channelResolver = channelResolver;
    }

    /// <summary>阶段4E·D3：按租户默认待办渠道(PLT租户.FDefaultTodoChannel)选注册渠道；解析不到/未注册则回退按待办自带渠道。</summary>
    private async Task<INotificationChannel?> ResolveChannelAsync(CfTodoItem todo)
    {
        var tenantChannels = await _channelResolver.ResolveChannelNamesAsync(todo.FTenantId);
        var channel = tenantChannels
            .Select(n => _channels.FirstOrDefault(c => c.ChannelName.Equals(n, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(c => c != null);
        // 回退：租户渠道解析不到或对应渠道未注册(如企微桩) → 用待办自带渠道
        if (channel == null && !string.IsNullOrWhiteSpace(todo.FPushChannel))
            channel = _channels.FirstOrDefault(c => c.ChannelName.Equals(todo.FPushChannel, StringComparison.OrdinalIgnoreCase));
        return channel;
    }

    public async Task DispatchCreateTodoAsync(long todoItemId)
    {
        var todo = await _dbContext.Set<CfTodoItem>().FirstOrDefaultAsync(t => t.FID == todoItemId);
        if (todo == null) return;

        var channel = await ResolveChannelAsync(todo);
        if (channel == null)
        {
            // 无可用渠道：待办本就未指定渠道=skipped；否则=指定渠道未注册=failed。
            var noChannel = string.IsNullOrWhiteSpace(todo.FPushChannel);
            if (!noChannel)
                _logger.LogWarning("推送渠道 {Channel} 未注册，TodoItemId={Id}", todo.FPushChannel, todoItemId);
            todo.FPushStatus = noChannel ? "skipped" : "failed";
            await _dbContext.SaveChangesAsync();
            return;
        }
        var channelName = channel.ChannelName;

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
            todo.FPushChannel = channelName;   // 记实际使用渠道(可能取自租户默认),供完成/删除用同一渠道
            todo.FPushStatus = "success";
            await _dbContext.SaveChangesAsync();

            // 阶段4E·R4：登记分发日志（taskId→租户/待办 权威绑定 + 幂等台账）。best-effort，绝不因日志失败翻掉已成功的推送。
            try
            {
                // externalTodoId 格式 orgId|unionId|taskId → 取末段 taskId
                var taskId = externalId?.Split('|').LastOrDefault();
                await _dispatchLog.RecordDispatchAsync(todo.FID, todo.FTenantId, channelName, taskId, corpId: null);
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "登记待办分发日志失败(best-effort)，TodoItemId={Id}", todoItemId);
            }
            return;
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
