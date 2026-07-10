using STOTOP.Module.CardFlow.Models;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class CcNotifyConfigTests
{
    [Fact]
    public void null或空JSON返回null不触发()
    {
        Assert.Null(CcNotifyConfig.Parse(null));
        Assert.Null(CcNotifyConfig.Parse(""));
        Assert.Null(CcNotifyConfig.Parse("   "));
    }

    [Fact]
    public void 非法JSON返回null不抛()
    {
        Assert.Null(CcNotifyConfig.Parse("{bad json"));
    }

    [Fact]
    public void users为空返回null不触发()
    {
        Assert.Null(CcNotifyConfig.Parse("""{"users":[],"timing":"onApprove","channels":["dingtalk"]}"""));
    }

    [Fact]
    public void 正常解析含timing和channels()
    {
        var cfg = CcNotifyConfig.Parse("""{"users":[{"userId":1,"userName":"A"}],"timing":"onApprove","channels":["system","dingtalk"]}""");
        Assert.NotNull(cfg);
        Assert.Single(cfg!.Users);
        Assert.Equal("onApprove", cfg.Timing);
        Assert.True(cfg.HasChannel("system"));
        Assert.True(cfg.HasChannel("dingtalk"));
        Assert.False(cfg.HasChannel("wecom"));
    }

    [Fact]
    public void 缺timing默认onEnter_缺channels默认system()
    {
        var cfg = CcNotifyConfig.Parse("""{"users":[{"userId":1,"userName":"A"}]}""");
        Assert.NotNull(cfg);
        Assert.Equal("onEnter", cfg!.Timing);
        Assert.True(cfg.HasChannel("system"));
        Assert.False(cfg.HasChannel("dingtalk"));
    }

    [Fact]
    public void ShouldFire匹配timing或always()
    {
        var cfg = CcNotifyConfig.Parse("""{"users":[{"userId":1,"userName":"A"}],"timing":"onApprove"}""");
        Assert.True(cfg!.ShouldFire("onApprove"));
        Assert.False(cfg.ShouldFire("onEnter"));
        Assert.False(cfg.ShouldFire("onReject"));

        var always = CcNotifyConfig.Parse("""{"users":[{"userId":1,"userName":"A"}],"timing":"always"}""");
        Assert.True(always!.ShouldFire("onEnter"));
        Assert.True(always.ShouldFire("onApprove"));
        Assert.True(always.ShouldFire("onReject"));
    }
}
