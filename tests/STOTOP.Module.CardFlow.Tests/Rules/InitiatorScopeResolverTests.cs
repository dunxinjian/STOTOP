using Microsoft.EntityFrameworkCore;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class InitiatorScopeResolverTests
{
    private const long UserId = 700;

    private static STOTOP.Infrastructure.Data.STOTOPDbContext Db(string name)
    {
        var db = TestDbContextFactory.Create(name);
        db.Set<SysUserRole>().Add(new SysUserRole { FID = 1, FUserId = UserId, FRoleId = 10 });
        db.Set<SysUserOrganization>().Add(new SysUserOrganization { FID = 1, FUserId = UserId, FOrgId = 20, FStatus = 1 });
        db.Set<SysUserPosition>().Add(new SysUserPosition { FID = 1, FUserId = UserId, FPositionId = 30 });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 空scope放行()
    {
        using var db = Db(nameof(空scope放行));
        var r = new InitiatorScopeResolver(db);
        var m = await r.GetUserMembershipsAsync(UserId);
        Assert.True(r.IsInScope(m, UserId, new InitiatorScope()));   // 全空=不限制
        Assert.True(r.IsInScope(m, UserId, null));                    // null=不限制
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 角色命中放行未命中拒绝()
    {
        using var db = Db(nameof(角色命中放行未命中拒绝));
        var r = new InitiatorScopeResolver(db);
        var m = await r.GetUserMembershipsAsync(UserId);
        Assert.True(r.IsInScope(m, UserId, new InitiatorScope { Roles = { 10 } }));
        Assert.False(r.IsInScope(m, UserId, new InitiatorScope { Roles = { 99 } }));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 组织岗位人员维度各自命中()
    {
        using var db = Db(nameof(组织岗位人员维度各自命中));
        var r = new InitiatorScopeResolver(db);
        var m = await r.GetUserMembershipsAsync(UserId);
        Assert.True(r.IsInScope(m, UserId, new InitiatorScope { Orgs = { 20 } }));
        Assert.True(r.IsInScope(m, UserId, new InitiatorScope { Positions = { 30 } }));
        Assert.True(r.IsInScope(m, UserId, new InitiatorScope { Users = { UserId } }));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task union任一维度命中即放行()
    {
        using var db = Db(nameof(union任一维度命中即放行));
        var r = new InitiatorScopeResolver(db);
        var m = await r.GetUserMembershipsAsync(UserId);
        // 角色不中(99) 但 组织中(20) → union 放行
        Assert.True(r.IsInScope(m, UserId, new InitiatorScope { Roles = { 99 }, Orgs = { 20 } }));
        // 全不中 → 拒绝
        Assert.False(r.IsInScope(m, UserId, new InitiatorScope { Roles = { 99 }, Orgs = { 88 }, Positions = { 77 }, Users = { 66 } }));
    }

    [Fact]
    public void 兼容读legacy可发起角色JSON派生角色维()
    {
        var p = StartPolicyCodec.Parse(null, "[\"10\",\"11\"]");
        Assert.NotNull(p.InitiatorScope);
        Assert.Equal(new List<long> { 10, 11 }, p.InitiatorScope!.Roles);
    }

    [Fact]
    public void 非法startPolicyJson_有legacy_不回退legacy仍不限制()
    {
        var p = StartPolicyCodec.Parse("{bad json", "[\"10\"]");
        Assert.True(p.InitiatorScope == null || p.InitiatorScope.IsEmpty);
    }

    [Fact]
    public void 非法startPolicyJson_无legacy_降级为不限制()
    {
        var p = StartPolicyCodec.Parse("{bad json", null);
        Assert.True(p.InitiatorScope == null || p.InitiatorScope.IsEmpty);
    }

    [Fact]
    public void 非法legacyJSON_单独_不抛且不限制()
    {
        var p = StartPolicyCodec.Parse(null, "{bad");
        Assert.True(p.InitiatorScope == null || p.InitiatorScope.IsEmpty);
    }
}
