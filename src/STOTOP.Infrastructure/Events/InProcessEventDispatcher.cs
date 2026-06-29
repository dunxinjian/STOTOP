using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace STOTOP.Infrastructure.Events;

public class InProcessEventDispatcher : IEventDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InProcessEventDispatcher> _logger;

    public InProcessEventDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<InProcessEventDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T @event) where T : BusinessEvent
    {
        if (@event == null)
        {
            _logger.LogWarning("试图发布空事件，已忽略");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<T>>().ToList();

        if (handlers.Count == 0)
        {
            _logger.LogDebug("事件 {EventType} 没有注册处理器", typeof(T).Name);
            return;
        }

        _logger.LogDebug("开始分发事件 {EventType}，处理器数量: {HandlerCount}，事件ID: {EventId}",
            typeof(T).Name, handlers.Count, @event.EventId);

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(@event);
            }
            catch (Exception ex)
            {
                // 记录异常但不中断其他处理器的执行
                _logger.LogError(ex, "处理事件 {EventType} 时发生错误，处理器: {HandlerType}，事件ID: {EventId}",
                    typeof(T).Name, handler.GetType().Name, @event.EventId);
            }
        }

        _logger.LogDebug("事件 {EventType} 分发完成，事件ID: {EventId}", typeof(T).Name, @event.EventId);
    }
}
