using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Redaction;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

/// <summary>M8-A 件②：CardService 消费发起范围——CreateAsync 校验 + GetAvailableFlowsAsync 过滤。</summary>
public class InitiatorScopeEnforcementTests
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
    public async global::System.Threading.Tasks.Task 发起人不在发起范围内_创建被拒()
    {
        using var db = TestDbContextFactory.Create(nameof(发起人不在发起范围内_创建被拒));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 3500,
            FFlowName = "范围流程",
            FFlowCode = "scope-flow",
            FOrgId = 1,
            FStatus = "published",
            FCreatorId = 1,
            FCreatedTime = DateTime.Now,
            FStartPolicyJson = """{"initiatorScope":{"roles":[10]}}""" // 仅角色10可发起
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 3501, FFlowDefinitionId = 3500, FStatus = "published", FIsCurrentVersion = true });
        // 用户 700 无角色10
        await db.SaveChangesAsync();

        var svc = BuildCardService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new CreateCardRequest { FlowDefinitionId = 3500, OrgId = 1, DataJson = "{}" }, userId: 700));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 发起范围为空_任何人可创建()
    {
        using var db = TestDbContextFactory.Create(nameof(发起范围为空_任何人可创建));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 3510, FFlowName = "开放流程", FFlowCode = "open-flow", FOrgId = 1, FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 3511, FFlowDefinitionId = 3510, FStatus = "published", FIsCurrentVersion = true });
        await db.SaveChangesAsync();

        var svc = BuildCardService(db);
        var card = await svc.CreateAsync(new CreateCardRequest { FlowDefinitionId = 3510, OrgId = 1, DataJson = "{}" }, userId: 700);
        Assert.NotNull(card);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 可发起清单按发起范围过滤_受限流程对无权限用户不可见()
    {
        using var db = TestDbContextFactory.Create(nameof(可发起清单按发起范围过滤_受限流程对无权限用户不可见));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 3520,
            FFlowName = "受限流程",
            FFlowCode = "restricted-flow",
            FOrgId = 1,
            FStatus = "published",
            FCreatorId = 1,
            FCreatedTime = DateTime.Now,
            FStartPolicyJson = """{"initiatorScope":{"roles":[10]}}""" // 用户 700 无角色10
        });
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 3521,
            FFlowName = "开放流程2",
            FFlowCode = "open-flow-2",
            FOrgId = 1,
            FStatus = "published",
            FCreatorId = 1,
            FCreatedTime = DateTime.Now
        });
        await db.SaveChangesAsync();

        var svc = BuildCardService(db);
        var flows = await svc.GetAvailableFlowsAsync(userId: 700, orgId: 1);

        Assert.DoesNotContain(flows, f => f.Id == 3520);
        Assert.Contains(flows, f => f.Id == 3521);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 可发起清单标记代提交_agentScope覆盖当前用户_onBehalfEnabled为真()
    {
        using var db = TestDbContextFactory.Create(nameof(可发起清单标记代提交_agentScope覆盖当前用户_onBehalfEnabled为真));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 3530,
            FFlowName = "代提交流程",
            FFlowCode = "onbehalf-flow-avail",
            FOrgId = 1,
            FStatus = "published",
            FCreatorId = 1,
            FCreatedTime = DateTime.Now,
            FStartPolicyJson = """{"onBehalf":{"enabled":true,"agentScope":{"users":[700]}}}""" // 用户700在 agentScope 内
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 3531, FFlowDefinitionId = 3530, FStatus = "published", FIsCurrentVersion = true });
        await db.SaveChangesAsync();

        var svc = BuildCardService(db);
        var flows = await svc.GetAvailableFlowsAsync(userId: 700, orgId: 1);

        var flow = Assert.Single(flows, f => f.Id == 3530);
        Assert.True(flow.OnBehalfEnabled);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 代提交已开启但agentScope为空_onBehalfEnabled为假()
    {
        using var db = TestDbContextFactory.Create(nameof(代提交已开启但agentScope为空_onBehalfEnabled为假));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 3540,
            FFlowName = "代提交流程-空范围",
            FFlowCode = "onbehalf-flow-avail-empty",
            FOrgId = 1,
            FStatus = "published",
            FCreatorId = 1,
            FCreatedTime = DateTime.Now,
            FStartPolicyJson = """{"onBehalf":{"enabled":true}}""" // 未填 agentScope → fail-closed，任何人都不可代提交
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 3541, FFlowDefinitionId = 3540, FStatus = "published", FIsCurrentVersion = true });
        await db.SaveChangesAsync();

        var svc = BuildCardService(db);
        var flows = await svc.GetAvailableFlowsAsync(userId: 700, orgId: 1);

        var flow = Assert.Single(flows, f => f.Id == 3540);
        Assert.False(flow.OnBehalfEnabled);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 未配置代提交策略_onBehalfEnabled为假()
    {
        using var db = TestDbContextFactory.Create(nameof(未配置代提交策略_onBehalfEnabled为假));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 3550, FFlowName = "普通流程", FFlowCode = "no-onbehalf-flow", FOrgId = 1, FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 3551, FFlowDefinitionId = 3550, FStatus = "published", FIsCurrentVersion = true });
        await db.SaveChangesAsync();

        var svc = BuildCardService(db);
        var flows = await svc.GetAvailableFlowsAsync(userId: 700, orgId: 1);

        var flow = Assert.Single(flows, f => f.Id == 3550);
        Assert.False(flow.OnBehalfEnabled);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task legacy可发起角色JSON_新列为空时_发起范围生效()
    {
        using var db = TestDbContextFactory.Create(nameof(legacy可发起角色JSON_新列为空时_发起范围生效));
        // 流程定义: 新列 F发起策略JSON=null, 旧列 FAllowedRolesJson=["10"]
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 3560,
            FFlowName = "legacy限制",
            FFlowCode = "legacy-restrict",
            FOrgId = 1,
            FStatus = "published",
            FCreatorId = 1,
            FCreatedTime = DateTime.Now,
            FStartPolicyJson = null,
            FAllowedRolesJson = "[\"10\"]"
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 3561, FFlowDefinitionId = 3560, FStatus = "published", FIsCurrentVersion = true });
        // 用户 700 无角色 10（即不在 legacy 限制内）
        await db.SaveChangesAsync();

        var svc = BuildCardService(db);
        // 应该被拒——legacy 角色限制生效
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new CreateCardRequest { FlowDefinitionId = 3560, OrgId = 1, DataJson = "{}" }, userId: 700));
    }
}
