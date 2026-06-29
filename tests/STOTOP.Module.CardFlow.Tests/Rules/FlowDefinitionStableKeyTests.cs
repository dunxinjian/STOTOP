using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class FlowDefinitionStableKeyTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task SaveDraftVersion_RejectsMissingStageKey()
    {
        using var db = TestDbContextFactory.Create(nameof(SaveDraftVersion_RejectsMissingStageKey));
        SeedDraft(db);
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveDraftVersionAsync(100, new SaveDraftVersionRequest
            {
                Stages =
                {
                    new StageDefinitionRequest { Name = "主管审批", SortOrder = 1, Type = "approval" }
                }
            }, operatorId: 1));

        Assert.Contains("稳定 StageKey", ex.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task SaveDraftVersion_RejectsDuplicateStageKeyInsteadOfRenamingIt()
    {
        using var db = TestDbContextFactory.Create(nameof(SaveDraftVersion_RejectsDuplicateStageKeyInsteadOfRenamingIt));
        SeedDraft(db);
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveDraftVersionAsync(100, new SaveDraftVersionRequest
            {
                Stages =
                {
                    new StageDefinitionRequest { StageKey = "manager", Name = "主管审批", SortOrder = 1, Type = "approval" },
                    new StageDefinitionRequest { StageKey = "manager", Name = "主管审批副本", SortOrder = 2, Type = "approval" }
                }
            }, operatorId: 1));

        Assert.Contains("重复", ex.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task SaveDraftVersion_DefaultsDynamicPolicyMaxInsertCountToTwenty()
    {
        using var db = TestDbContextFactory.Create(nameof(SaveDraftVersion_DefaultsDynamicPolicyMaxInsertCountToTwenty));
        SeedDraft(db);
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);

        var detail = await service.SaveDraftVersionAsync(100, new SaveDraftVersionRequest
        {
            Stages =
            {
                new StageDefinitionRequest
                {
                    StageKey = "manager",
                    Name = "主管审批",
                    SortOrder = 1,
                    Type = "human"
                }
            },
            DynamicPolicies =
            {
                new DynamicStagePolicyRequest
                {
                    PolicyKey = "gm_approval",
                    SourceStageKey = "manager",
                    PolicyName = "追加总经理",
                    StrategyType = "fixedUsers",
                    StrategyConfigJson = """{"users":[{"userId":7,"userName":"总经理"}]}""",
                    TriggerTiming = "afterSourceBeforeRoute",
                    Priority = 1
                }
            }
        }, operatorId: 1);

        Assert.Single(detail.DynamicPolicies);
        Assert.Equal(20, detail.DynamicPolicies[0].MaxInsertCount);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task SaveDraftVersion_DefaultsDynamicPolicyTriggerTimingToAfterSourceBeforeRoute()
    {
        using var db = TestDbContextFactory.Create(nameof(SaveDraftVersion_DefaultsDynamicPolicyTriggerTimingToAfterSourceBeforeRoute));
        SeedDraft(db);
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);

        var detail = await service.SaveDraftVersionAsync(100, new SaveDraftVersionRequest
        {
            Stages =
            {
                new StageDefinitionRequest
                {
                    StageKey = "manager",
                    Name = "主管审批",
                    SortOrder = 1,
                    Type = "human"
                }
            },
            DynamicPolicies =
            {
                new DynamicStagePolicyRequest
                {
                    PolicyKey = "gm_approval",
                    SourceStageKey = "manager",
                    PolicyName = "追加总经理",
                    StrategyType = "fixedUsers",
                    StrategyConfigJson = """{"users":[{"userId":7,"userName":"总经理"}]}""",
                    Priority = 1
                }
            }
        }, operatorId: 1);

        Assert.Single(detail.DynamicPolicies);
        Assert.Equal("afterSourceBeforeRoute", detail.DynamicPolicies[0].TriggerTiming);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Publish_RejectsDynamicPolicyWithoutFallback()
    {
        using var db = TestDbContextFactory.Create(nameof(Publish_RejectsDynamicPolicyWithoutFallback));
        SeedDraft(db);
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        await service.SaveDraftVersionAsync(100, new SaveDraftVersionRequest
        {
            Stages =
            {
                new StageDefinitionRequest { StageKey = "manager", Name = "主管审批", SortOrder = 1, Type = "human" }
            },
            DynamicPolicies =
            {
                new DynamicStagePolicyRequest
                {
                    PolicyKey = "gm_approval",
                    SourceStageKey = "manager",
                    PolicyName = "追加总经理",
                    StrategyType = "fixedUsers",
                    StrategyConfigJson = """{"users":[{"userId":7,"userName":"总经理"}]}""",
                    TriggerTiming = "afterSourceBeforeRoute",
                    Priority = 1
                }
            }
        }, operatorId: 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishAsync(100, operatorId: 1));

        Assert.Contains("兜底", ex.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Publish_RejectsAfterRouteBeforeTargetPolicyWithoutContinuation()
    {
        using var db = TestDbContextFactory.Create(nameof(Publish_RejectsAfterRouteBeforeTargetPolicyWithoutContinuation));
        SeedDraft(db);
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        await service.SaveDraftVersionAsync(100, new SaveDraftVersionRequest
        {
            Stages =
            {
                new StageDefinitionRequest { StageKey = "manager", Name = "主管审批", SortOrder = 1, Type = "human" },
                new StageDefinitionRequest { StageKey = "finance", Name = "财务复核", SortOrder = 2, Type = "human" }
            },
            Routes =
            {
                new StageRouteRuleRequest
                {
                    EdgeKey = "manager_to_finance",
                    FromStageKey = "manager",
                    ToStageKey = "finance",
                    RouteName = "默认",
                    Priority = 1,
                    IsDefault = true
                }
            },
            DynamicPolicies =
            {
                new DynamicStagePolicyRequest
                {
                    PolicyKey = "gm_approval",
                    SourceStageKey = "manager",
                    PolicyName = "追加总经理",
                    StrategyType = "fixedUsers",
                    StrategyConfigJson = """{"users":[{"userId":7,"userName":"总经理"}]}""",
                    TriggerTiming = "afterRouteBeforeTarget",
                    Priority = 1,
                    FallbackJson = """{"type":"fixedUsers","users":[{"userId":1,"userName":"管理员"}]}"""
                }
            }
        }, operatorId: 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishAsync(100, operatorId: 1));

        Assert.Contains("续接节点", ex.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Publish_AcceptsLegacyAfterCompleteDynamicPolicyAsAfterSourceBeforeRoute()
    {
        using var db = TestDbContextFactory.Create(nameof(Publish_AcceptsLegacyAfterCompleteDynamicPolicyAsAfterSourceBeforeRoute));
        SeedDraft(db);
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 300,
            FFlowVersionId = 200,
            FStageKey = "manager",
            FStageName = "主管审批",
            FSortOrder = 1,
            FType = "human"
        });
        db.Set<CfDynamicStagePolicy>().Add(new CfDynamicStagePolicy
        {
            FID = 400,
            FFlowVersionId = 200,
            FPolicyKey = "legacy_gm_approval",
            FSourceStageDefinitionId = 300,
            FSourceStageKey = "manager",
            FPolicyName = "历史追加总经理",
            FStrategyType = "fixedUsers",
            FStrategyConfigJson = """{"users":[{"userId":7,"userName":"总经理"}]}""",
            FTriggerTiming = "afterComplete",
            FPriority = 1,
            FMaxInsertCount = 20,
            FFallbackJson = """{"type":"fixedUsers","users":[{"userId":1,"userName":"管理员"}]}""",
            FStatus = "active"
        });
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);

        await service.PublishAsync(100, operatorId: 1);

        var published = await db.Set<CfFlowVersion>().FindAsync(200L);
        Assert.NotNull(published);
        Assert.True(published!.FIsCurrentVersion);
    }

    private static void SeedDraft(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 100,
            FFlowName = "费用报销",
            FFlowCode = "FYBS",
            FStatus = "draft",
            FOrgId = 1,
            FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 200,
            FFlowDefinitionId = 100,
            FVersionNumber = 1,
            FStatus = "draft",
            FCreatedTime = DateTime.Now
        });
    }
}
