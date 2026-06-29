using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.CRM.Tests;

// STOTOP.Module 下同时有 Task 与 System 子命名空间，会与 System.Threading.Tasks.Task 撞名；
// 在文件作用域命名空间「之后」用 global:: 声明别名消除歧义（泛型 Task<T> 不受影响）。
using Task = global::System.Threading.Tasks.Task;

/// <summary>CRM 服务测试替身：日志用 NullLogger；事件分发器用可计数 no-op，便于断言「是否发布过领域事件」。</summary>
public static class CrmTestFakes
{
    public static ILogger<T> Logger<T>() => NullLogger<T>.Instance;

    /// <summary>记录事件发布次数与最后一次事件，不产生真实副作用。</summary>
    public sealed class CountingEventDispatcher : IEventDispatcher
    {
        public int PublishCount { get; private set; }
        public BusinessEvent? LastEvent { get; private set; }

        public Task PublishAsync<T>(T @event) where T : BusinessEvent
        {
            PublishCount++;
            LastEvent = @event;
            return Task.CompletedTask;
        }
    }
}
