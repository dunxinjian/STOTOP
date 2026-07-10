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
}
