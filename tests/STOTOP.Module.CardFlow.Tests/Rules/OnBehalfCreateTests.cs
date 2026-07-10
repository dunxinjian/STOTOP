using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Redaction;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

/// <summary>M8-A 件③：CreateAsync 代提交(onBehalf)——越权护栏 + 被代理人/代理人留痕。</summary>
public class OnBehalfCreateTests
{
    private static CardService BuildCardService(STOTOP.Infrastructure.Data.STOTOPDbContext db) => new(
        db,
        NullLogger<CardService>.Instance,
        new StageConfigParser(),
        new StageViewProfileResolver(new CardPresentationResolver()),
        new CardFlowSourceContextVerifier(db),
        new CardRedactionService(),
        new InitiatorScopeResolver(db));

    [Fact]
    public async global::System.Threading.Tasks.Task 授权代提交_发起人为被代理人代理人留痕()
    {
        using var db = TestDbContextFactory.Create(nameof(授权代提交_发起人为被代理人代理人留痕));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 3600,
            FFlowName = "代提交流程",
            FFlowCode = "onbehalf-flow",
            FOrgId = 1,
            FStatus = "published",
            FCreatorId = 1,
            FCreatedTime = DateTime.Now,
            FStartPolicyJson = """{"onBehalf":{"enabled":true,"agentScope":{"users":[900]}}}""" // 用户900可代提交
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 3601, FFlowDefinitionId = 3600, FStatus = "published", FIsCurrentVersion = true });
        db.Set<SysUser>().Add(new SysUser { FID = 900, FName = "代理人" });
        db.Set<SysUser>().Add(new SysUser { FID = 901, FName = "被代理人" });
        await db.SaveChangesAsync();

        var svc = BuildCardService(db);
        var card = await svc.CreateAsync(new CreateCardRequest { FlowDefinitionId = 3600, OrgId = 1, DataJson = "{}", ActualInitiatorId = 901 }, userId: 900);

        var saved = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == card.Id);
        Assert.Equal(901, saved.FInitiatorId);
        Assert.Equal("被代理人", saved.FInitiatorName);
        Assert.Equal(900, saved.FAgentId);
        Assert.Equal("代理人", saved.FAgentName);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 未授权代提交_被拒()
    {
        using var db = TestDbContextFactory.Create(nameof(未授权代提交_被拒));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 3610,
            FFlowName = "代提交流程",
            FFlowCode = "onbehalf-flow2",
            FOrgId = 1,
            FStatus = "published",
            FCreatorId = 1,
            FCreatedTime = DateTime.Now,
            FStartPolicyJson = """{"onBehalf":{"enabled":true,"agentScope":{"users":[900]}}}"""
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 3611, FFlowDefinitionId = 3610, FStatus = "published", FIsCurrentVersion = true });
        await db.SaveChangesAsync();
        var svc = BuildCardService(db);
        // 用户 902 不在 agentScope
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new CreateCardRequest { FlowDefinitionId = 3610, OrgId = 1, DataJson = "{}", ActualInitiatorId = 901 }, userId: 902));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task onBehalf未开启却传ActualInitiator_被拒()
    {
        using var db = TestDbContextFactory.Create(nameof(onBehalf未开启却传ActualInitiator_被拒));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 3620, FFlowName = "普通流程", FFlowCode = "normal-flow", FOrgId = 1, FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 3621, FFlowDefinitionId = 3620, FStatus = "published", FIsCurrentVersion = true });
        await db.SaveChangesAsync();
        var svc = BuildCardService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new CreateCardRequest { FlowDefinitionId = 3620, OrgId = 1, DataJson = "{}", ActualInitiatorId = 901 }, userId: 900));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task onBehalf开启但agentScope为空_任何人代提交都被拒()
    {
        using var db = TestDbContextFactory.Create(nameof(onBehalf开启但agentScope为空_任何人代提交都被拒));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 3630,
            FFlowName = "代提交流程-空范围",
            FFlowCode = "onbehalf-flow-empty-scope",
            FOrgId = 1,
            FStatus = "published",
            FCreatorId = 1,
            FCreatedTime = DateTime.Now,
            FStartPolicyJson = """{"onBehalf":{"enabled":true}}""" // 未填 agentScope → 空范围，应等于"无人可代提交"
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 3631, FFlowDefinitionId = 3630, FStatus = "published", FIsCurrentVersion = true });
        db.Set<SysUser>().Add(new SysUser { FID = 901, FName = "被代理人" });
        await db.SaveChangesAsync();

        var svc = BuildCardService(db);
        // 任意操作人（903）尝试代 901 提交：agentScope 为空须 fail-closed 拒绝，而非放行
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new CreateCardRequest { FlowDefinitionId = 3630, OrgId = 1, DataJson = "{}", ActualInitiatorId = 901 }, userId: 903));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 被代理发起人不存在_被拒()
    {
        using var db = TestDbContextFactory.Create(nameof(被代理发起人不存在_被拒));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 3640,
            FFlowName = "代提交流程",
            FFlowCode = "onbehalf-flow-noexist",
            FOrgId = 1,
            FStatus = "published",
            FCreatorId = 1,
            FCreatedTime = DateTime.Now,
            FStartPolicyJson = """{"onBehalf":{"enabled":true,"agentScope":{"users":[900]}}}"""
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 3641, FFlowDefinitionId = 3640, FStatus = "published", FIsCurrentVersion = true });
        db.Set<SysUser>().Add(new SysUser { FID = 900, FName = "代理人" });
        // 901（被代理人）不存在
        await db.SaveChangesAsync();

        var svc = BuildCardService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new CreateCardRequest { FlowDefinitionId = 3640, OrgId = 1, DataJson = "{}", ActualInitiatorId = 901 }, userId: 900));
    }

    /// <summary>M8-A 终审修复：initiatorScope 须校验【被代理人】(有效发起人)，代理人只受 agentScope 约束——
    /// 代理人不在 initiatorScope 内不应被拒（旧代码先校验 operator 会误挡"秘书代经理发起"场景）。</summary>
    [Fact]
    public async global::System.Threading.Tasks.Task 代提交_被代理人在发起范围内_代理人不在_放行()
    {
        using var db = TestDbContextFactory.Create(nameof(代提交_被代理人在发起范围内_代理人不在_放行));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 3650,
            FFlowName = "代提交+发起范围流程",
            FFlowCode = "onbehalf-initiator-scope-flow",
            FOrgId = 1,
            FStatus = "published",
            FCreatorId = 1,
            FCreatedTime = DateTime.Now,
            FStartPolicyJson = """{"initiatorScope":{"roles":[10]},"onBehalf":{"enabled":true,"agentScope":{"users":[900]}}}"""
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 3651, FFlowDefinitionId = 3650, FStatus = "published", FIsCurrentVersion = true });
        db.Set<SysUser>().Add(new SysUser { FID = 900, FName = "代理人" });
        db.Set<SysUser>().Add(new SysUser { FID = 901, FName = "被代理人" });
        db.Set<SysUserRole>().Add(new SysUserRole { FUserId = 901, FRoleId = 10 }); // 被代理人(有效发起人)持有角色10
        // 900（代理人）不持有角色10——发起范围只应针对被代理人校验，代理人不应因不在发起范围内被拒
        await db.SaveChangesAsync();

        var svc = BuildCardService(db);
        var card = await svc.CreateAsync(new CreateCardRequest { FlowDefinitionId = 3650, OrgId = 1, DataJson = "{}", ActualInitiatorId = 901 }, userId: 900);

        var saved = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == card.Id);
        Assert.Equal(901, saved.FInitiatorId);
        Assert.Equal(900, saved.FAgentId);
    }

    /// <summary>被代理人(有效发起人)不在 initiatorScope 内须被拒——即便代理人自身在 agentScope 内也持有该角色，
    /// 旧代码只校验 operator 会让此场景误放行（被代理人的发起范围从未被校验）。</summary>
    [Fact]
    public async global::System.Threading.Tasks.Task 代提交_被代理人不在发起范围内_被拒()
    {
        using var db = TestDbContextFactory.Create(nameof(代提交_被代理人不在发起范围内_被拒));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 3660,
            FFlowName = "代提交+发起范围流程2",
            FFlowCode = "onbehalf-initiator-scope-flow2",
            FOrgId = 1,
            FStatus = "published",
            FCreatorId = 1,
            FCreatedTime = DateTime.Now,
            FStartPolicyJson = """{"initiatorScope":{"roles":[10]},"onBehalf":{"enabled":true,"agentScope":{"users":[900]}}}"""
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 3661, FFlowDefinitionId = 3660, FStatus = "published", FIsCurrentVersion = true });
        db.Set<SysUser>().Add(new SysUser { FID = 900, FName = "代理人" });
        db.Set<SysUser>().Add(new SysUser { FID = 902, FName = "被代理人-无权限" });
        db.Set<SysUserRole>().Add(new SysUserRole { FUserId = 900, FRoleId = 10 }); // 代理人自身持有角色10——用以暴露"旧代码只校验代理人"会误放行
        // 902（被代理人/有效发起人）不持有角色10——应被拒
        await db.SaveChangesAsync();

        var svc = BuildCardService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new CreateCardRequest { FlowDefinitionId = 3660, OrgId = 1, DataJson = "{}", ActualInitiatorId = 902 }, userId: 900));
    }
}
