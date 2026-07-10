using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class FlowDefinitionRollbackDraftTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task 回滚_已存在草稿时拒绝()
    {
        using var db = TestDbContextFactory.Create(nameof(回滚_已存在草稿时拒绝));
        SeedPublishedV1(db);
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 202, FFlowDefinitionId = 100, FVersionNumber = 2,
            FStatus = "draft", FCreatedTime = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateDraftFromVersionAsync(100, 201, operatorId: 1));

        Assert.Contains("已存在未发布草稿", ex.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 回滚_无草稿时从历史版本快照建草稿且内容一致()
    {
        using var db = TestDbContextFactory.Create(nameof(回滚_无草稿时从历史版本快照建草稿且内容一致));
        SeedPublishedV1(db);
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var draft = await service.CreateDraftFromVersionAsync(100, 201, operatorId: 7);

        Assert.Equal("draft", draft.Status);
        Assert.Equal(2, draft.VersionNumber);
        Assert.Equal("""{"fields":[{"key":"amount"}]}""", draft.CardSchemaJson);
        Assert.Equal("""{"approvalAdminUserIds":[9]}""", draft.FlowSettingsJson);

        // 节点/路由快照一致
        Assert.Equal(2, draft.Stages.Count);
        Assert.Equal(new[] { "manager", "finance" }, draft.Stages.Select(s => s.StageKey).ToArray());
        Assert.Equal("财务复核", draft.Stages[1].StageName);
        var route = Assert.Single(draft.Routes);
        Assert.Equal("edge_default", route.EdgeKey);
        Assert.Equal("manager", route.FromStageKey);
        Assert.Equal("finance", route.ToStageKey);
        Assert.True(route.IsDefault);

        // 源版本内容不动
        var source = await db.Set<CfFlowVersion>().AsNoTracking().FirstAsync(v => v.FID == 201);
        Assert.Equal("published", source.FStatus);
        Assert.True(source.FIsCurrentVersion);

        // ratio 模式的 FApprovalThreshold 须随克隆节点一并拷贝（M8-C 件④ review）
        var clonedManagerStage = await db.Set<CfStageDefinition>().AsNoTracking()
            .FirstAsync(s => s.FFlowVersionId == draft.Id && s.FStageKey == "manager");
        Assert.Equal("ratio", clonedManagerStage.FApprovalMode);
        Assert.Equal(60, clonedManagerStage.FApprovalThreshold);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 回滚_指定版本不存在时拒绝()
    {
        using var db = TestDbContextFactory.Create(nameof(回滚_指定版本不存在时拒绝));
        SeedPublishedV1(db);
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateDraftFromVersionAsync(100, 999, operatorId: 1));

        Assert.Contains("历史版本不存在", ex.Message);
    }

    private static void SeedPublishedV1(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 100, FFlowName = "费用报销", FFlowCode = "FYBS",
            FStatus = "published", FOrgId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 201, FFlowDefinitionId = 100, FVersionNumber = 1,
            FStatus = "published", FIsCurrentVersion = true,
            FCardSchemaJson = """{"fields":[{"key":"amount"}]}""",
            FFlowSettingsJson = """{"approvalAdminUserIds":[9]}""",
            FPublishTime = DateTime.Now, FCreatedTime = DateTime.Now
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 301, FFlowVersionId = 201, FStageKey = "manager",
            FStageName = "主管审批", FSortOrder = 1, FType = "human",
            FApprovalMode = "ratio", FApprovalThreshold = 60
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 302, FFlowVersionId = 201, FStageKey = "finance",
            FStageName = "财务复核", FSortOrder = 2, FType = "human"
        });
        db.Set<CfStageRouteRule>().Add(new CfStageRouteRule
        {
            FID = 401, FFlowVersionId = 201, FEdgeKey = "edge_default",
            FFromStageDefinitionId = 301, FFromStageKey = "manager",
            FToStageDefinitionId = 302, FToStageKey = "finance",
            FRouteName = "默认", FPriority = 1, FIsDefault = true, FStatus = "active"
        });
    }
}
