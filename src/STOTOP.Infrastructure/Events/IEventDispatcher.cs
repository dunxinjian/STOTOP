namespace STOTOP.Infrastructure.Events;

/// <summary>
/// 事件分发器接口 - 用于发布业务事件
/// </summary>
public interface IEventDispatcher
{
    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="T">事件类型</typeparam>
    /// <param name="event">事件实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PublishAsync<T>(T @event) where T : BusinessEvent;
}
