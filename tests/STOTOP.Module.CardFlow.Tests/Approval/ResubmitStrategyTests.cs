using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class ResubmitStrategyTests
{
    private const long DefId = 3400, VerId = 3401, StageA = 6201, StageB = 6202, Initiator = 88;

    [Fact]
    public async global::System.Threading.Tasks.Task 重提fromRejected从被驳回节点续跑()
    {
        using var db = CreateDb(nameof(重提fromRejected从被驳回节点续跑), """{"resubmitStrategy":"fromRejected"}""");
        SeedReturnedCardRejectedAtB(db);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();

        var result = await CreateEngine(db).ResubmitAsync(9710, Initiator);
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var newStage = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9710 && s.FRound == 2);
        Assert.Equal(StageB, newStage.FStageDefinitionId); // 回到被驳回的 B，非 A
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 重提fromStart默认回到首节点()
    {
        using var db = CreateDb(nameof(重提fromStart默认回到首节点), null); // 无策略=缺省 fromStart
        SeedReturnedCardRejectedAtB(db);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();

        var result = await CreateEngine(db).ResubmitAsync(9710, Initiator);
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var newStage = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9710 && s.FRound == 2);
        Assert.Equal(StageA, newStage.FStageDefinitionId); // 回到首节点 A
    }

    private static STOTOP.Infrastructure.Data.STOTOPDbContext CreateDb(string name, string? settingsJson)
    {
        var db = TestDbContextFactory.Create(name);
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = DefId, FFlowName = "重提策略", FFlowCode = "resubmit-strategy", FOrgId = 1, FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = VerId, FFlowDefinitionId = DefId, FStatus = "published", FIsCurrentVersion = true, FFlowSettingsJson = settingsJson });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition { FID = StageA, FFlowVersionId = VerId, FSortOrder = 1, FStageName = "A", FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers", FAssigneeConfigJson = """{"users":[{"userId":51,"userName":"审批人"}]}""" });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition { FID = StageB, FFlowVersionId = VerId, FSortOrder = 2, FStageName = "B", FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers", FAssigneeConfigJson = """{"users":[{"userId":51,"userName":"审批人"}]}""" });
        db.Set<SysUser>().Add(new SysUser { FID = 51, FName = "审批人" });
        return db;
    }

    private static void SeedReturnedCardRejectedAtB(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfCard>().Add(new CfCard { FID = 9710, FFlowDefinitionId = DefId, FFlowVersionId = VerId, FTitle = "重提", FStatus = "returned", FInitiatorId = Initiator, FInitiatorName = "发起人", FCurrentRound = 1, FOrgId = 1, FDataJson = "{}" });
        db.Set<CfStageInstance>().Add(new CfStageInstance { FID = 9810, FCardId = 9710, FStageDefinitionId = StageB, FStageName = "B", FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "returned", FFinalAction = "rejected", FCompletedTime = DateTime.Now });
    }

    // CreateEngine：整体照抄 FlowActionNoTrackingPersistenceTests.CreateEngine（同一 fakes 装配）
    private static FlowEngineService CreateEngine(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var orchestration = new OrchestrationEngineService(db, NullLogger<OrchestrationEngineService>.Instance);
        return new FlowEngineService(db, new FakeNumberSequenceService(), new FakeCardSchemaService(),
            new ApprovalModeHandler(), new SequentialApprovalRuntime(), new ReturnToStageRuntime(),
            new StageConfigParser(), new StageFieldAccessService(), new StageActionPolicyService(),
            new ConditionRuleEvaluator(), new ApproverResolver(db), new FakeBudgetOccupationService(),
            new DbTodoService(db), new FakeNotificationDispatcher(), new AutoPluginFactory(provider),
            provider, provider.GetRequiredService<IServiceScopeFactory>(), orchestration,
            new FakeBatchNotifier(), new FakeBatchLifecycleService(), NullLogger<FlowEngineService>.Instance);
    }
}
