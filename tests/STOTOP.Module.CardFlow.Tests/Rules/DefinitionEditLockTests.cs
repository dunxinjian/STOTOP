using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class DefinitionEditLockTests
{
    private static DefinitionEditLockService CreateService(string testName, long? orgId = 1)
    {
        var db = TestDbContextFactory.Create(testName, orgId);
        // 种一个流程定义供锁引用（全局过滤器需 FOrgId 匹配）
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 100, FFlowName = "测试流程", FFlowCode = "TEST",
            FStatus = "draft", FOrgId = orgId ?? 1, FTenantId = 1, FCreatedTime = DateTime.Now
        });
        db.SaveChanges();
        return new DefinitionEditLockService(db, hubContext: null);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 无锁时获取成功成为持锁人()
    {
        var svc = CreateService(nameof(无锁时获取成功成为持锁人));

        var result = await svc.AcquireAsync(100, userId: 1, userName: "张三");

        Assert.True(result.Held);
        Assert.Equal(1, result.HolderId);
        Assert.Equal("张三", result.HolderName);
        Assert.True(result.IsSelf);
        Assert.Null(result.Takeover);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 他人活锁时获取返回只读且带持锁人信息()
    {
        var svc = CreateService(nameof(他人活锁时获取返回只读且带持锁人信息));

        // 用户 1 先获取锁
        await svc.AcquireAsync(100, userId: 1, userName: "张三");

        // 用户 2 尝试获取 → 返回只读
        var result = await svc.AcquireAsync(100, userId: 2, userName: "李四");

        Assert.True(result.Held);
        Assert.Equal(1, result.HolderId);
        Assert.Equal("张三", result.HolderName);
        Assert.False(result.IsSelf);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 心跳超时后锁可被抢占()
    {
        var db = TestDbContextFactory.Create(nameof(心跳超时后锁可被抢占), 1);
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 100, FFlowName = "测试流程", FFlowCode = "TEST",
            FStatus = "draft", FOrgId = 1, FTenantId = 1, FCreatedTime = DateTime.Now
        });
        // 手动插入一行过期锁（心跳时间回拨 3 分钟）
        db.Set<CfDefinitionEditLock>().Add(new CfDefinitionEditLock
        {
            FFlowDefinitionId = 100,
            FHolderId = 1, FHolderName = "张三",
            FAcquiredTime = DateTime.Now.AddMinutes(-10),
            FHeartbeatAt = DateTime.Now.AddMinutes(-3), // 超过 120s
            FOrgId = 1, FTenantId = 1,
        });
        db.SaveChanges();

        var svc = new DefinitionEditLockService(db, hubContext: null);

        // 用户 2 获取 → 抢占成功
        var result = await svc.AcquireAsync(100, userId: 2, userName: "李四");

        Assert.True(result.Held);
        Assert.Equal(2, result.HolderId);
        Assert.Equal("李四", result.HolderName);
        Assert.True(result.IsSelf);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 并发接管仅一胜_第二人被拒()
    {
        var svc = CreateService(nameof(并发接管仅一胜_第二人被拒));

        // 用户 1 持锁
        await svc.AcquireAsync(100, userId: 1, userName: "张三");

        // 用户 2 申请接管 → 成功
        var r1 = await svc.RequestTakeoverAsync(100, requesterId: 2, requesterName: "李四");
        Assert.NotNull(r1.Takeover);
        Assert.Equal(2, r1.Takeover!.RequesterId);

        // 用户 3 申请接管 → 被拒（已有请求处理中）
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RequestTakeoverAsync(100, requesterId: 3, requesterName: "王五"));
        Assert.Contains("已有接管请求处理中", ex.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 同意接管后持锁人原子移交为申请人()
    {
        var svc = CreateService(nameof(同意接管后持锁人原子移交为申请人));

        // 用户 1 持锁
        await svc.AcquireAsync(100, userId: 1, userName: "张三");
        // 用户 2 申请接管
        await svc.RequestTakeoverAsync(100, requesterId: 2, requesterName: "李四");

        // 用户 1 同意
        var result = await svc.RespondTakeoverAsync(100, holderId: 1, accept: true);

        // 锁已移交给用户 2
        Assert.True(result.Held);
        Assert.Equal(2, result.HolderId);
        Assert.Equal("李四", result.HolderName);
        Assert.False(result.IsSelf); // 从用户 1 视角看不再是 self
        Assert.Null(result.Takeover); // 请求段已清空
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 拒绝接管清空请求且不移交()
    {
        var svc = CreateService(nameof(拒绝接管清空请求且不移交));

        // 用户 1 持锁
        await svc.AcquireAsync(100, userId: 1, userName: "张三");
        // 用户 2 申请接管
        await svc.RequestTakeoverAsync(100, requesterId: 2, requesterName: "李四");

        // 用户 1 拒绝
        var result = await svc.RespondTakeoverAsync(100, holderId: 1, accept: false);

        // 锁仍在用户 1
        Assert.True(result.Held);
        Assert.Equal(1, result.HolderId);
        Assert.True(result.IsSelf);
        Assert.Null(result.Takeover); // 请求段已清空
    }
}
