using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;

namespace STOTOP.Infrastructure.Events;

// 注：注册为 Scoped（非 Singleton）——需注入当前作用域的 IOrgContextAccessor 以把发布方的
// 组织/租户上下文传播进事件处理器的子作用域（否则非 HTTP 来源如 Hangfire 任务发布的事件，
// 处理器在全新子作用域里丢租户上下文 → 读 ITenantScoped 空集/写抛）。无单例消费者，改 Scoped 安全。
public class InProcessEventDispatcher : IEventDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOrgContextAccessor _orgContext;
    private readonly ILogger<InProcessEventDispatcher> _logger;

    public InProcessEventDispatcher(
        IServiceScopeFactory scopeFactory,
        IOrgContextAccessor orgContext,
        ILogger<InProcessEventDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _orgContext = orgContext;
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

        // 把发布方（当前作用域）的组织/租户/平台上下文传播进处理器子作用域。
        // HTTP 来源经 HttpContext.Items 本就跨作用域可见，重设为同值无害；非 HTTP 来源（Hangfire 任务等）
        // 子作用域的 IOrgContextAccessor 是全新实例、无上下文，靠此显式传播过 fail-closed 租户硬墙。
        var innerContext = scope.ServiceProvider.GetService<IOrgContextAccessor>();
        if (innerContext != null)
        {
            innerContext.CurrentOrgId = _orgContext.CurrentOrgId;
            innerContext.CurrentTenantId = _orgContext.CurrentTenantId;
            innerContext.IsPlatformScope = _orgContext.IsPlatformScope;
        }

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
