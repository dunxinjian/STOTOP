namespace STOTOP.Infrastructure.Events;

/// <summary>
/// 事件处理器接口 - 处理特定类型的事件
/// </summary>
/// <typeparam name="T">事件类型</typeparam>
public interface IEventHandler<T> where T : BusinessEvent
{
    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="event">事件实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task HandleAsync(T @event, CancellationToken cancellationToken = default);
}
