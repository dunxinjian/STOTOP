using Microsoft.AspNetCore.Http;
using STOTOP.Module.System.Services;
using Xunit;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// HttpOrgContextAccessor 的 override 走 AsyncLocal 自检：
/// 后台/非 HTTP 场景经 setter 设的组织/租户上下文，随异步执行流【穿透子 DI 作用域的新实例】——
/// 这是"事件处理器/回调/插件等子作用域在 fail-closed 下不丢租户上下文"的机制基础；同时按执行流隔离、不跨流泄漏。
/// </summary>
public class AsyncLocalContextTests
{
    private sealed class NullHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private static HttpOrgContextAccessor NewAccessor() => new(new NullHttpContextAccessor());

    [Fact]
    public void 子作用域新实例经AsyncLocal读到发布方设置的上下文()
    {
        var outer = NewAccessor();
        outer.ClearOverride();
        outer.CurrentTenantId = 42;
        outer.CurrentOrgId = 7;
        outer.IsPlatformScope = true;

        // 模拟子 DI 作用域：全新实例，读同一静态 AsyncLocal（同一执行流）
        var child = NewAccessor();
        Assert.Equal(42, child.CurrentTenantId);
        Assert.Equal(7, child.CurrentOrgId);
        Assert.True(child.IsPlatformScope);

        outer.ClearOverride();
        Assert.Null(NewAccessor().CurrentTenantId);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 子异步流继承父上下文且子流修改不回灌父流()
    {
        var outer = NewAccessor();
        outer.ClearOverride();
        outer.CurrentTenantId = 10;

        await global::System.Threading.Tasks.Task.Run(() =>
        {
            var inner = NewAccessor();
            Assert.Equal(10, inner.CurrentTenantId);   // 子流继承父上下文
            inner.CurrentTenantId = 20;                // 子流内改
            Assert.Equal(20, inner.CurrentTenantId);
        });

        // 子流的修改不回灌父流（AsyncLocal 只沿流向下传播）
        Assert.Equal(10, outer.CurrentTenantId);
        outer.ClearOverride();
    }
}
