using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

/// <summary>M5-2 失败态推演：干跑遇失败不终止，按兜底/失败策略标注并继续（或标注为终点）。</summary>
public class CardFlowPathPreviewFailureTests
{
    private static CardFlowPathPreviewService CreateService(STOTOP.Infrastructure.Data.STOTOPDbContext db)
        => new(db, new ConditionRuleEvaluator(), new AuditSnapshotPolicyService(), new ApproverResolver(db));

    [Fact]
    public async global::System.Threading.Tasks.Task PreviewDraftVersion_部门无主管兜底_标注assigneeUnresolved且推演继续到终点()
    {
        using var db = TestDbContextFactory.Create(nameof(PreviewDraftVersion_部门无主管兜底_标注assigneeUnresolved且推演继续到终点));

        // 组织 50 无主管（FManagerId 空）→ orgChain 策略解析不到处理人 → 触发 fixedUsers 兜底
        db.Set<SysOrganization>().Add(new SysOrganization { FID = 50, FParentId = 0, FCode = "NOMGR", FName = "无主管部门", FStatus = 1, FManagerId = null });
        db.Set<SysUser>().Add(new SysUser { FID = 99, FName = "兜底审批人", FStatus = 1 });

        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 500, FFlowName = "无主管兜底", FFlowCode = "NMBD", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 501, FFlowDefinitionId = 500, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition
            {
                FID = 60, FFlowVersionId = 501, FStageKey = "approve", FStageName = "部门审批", FType = "human", FSortOrder = 1,
                FAssigneeStrategy = "orgChain",
                FAssigneeConfigJson = """{"startOrgId":50,"fallback":{"type":"fixedUsers","users":[{"userId":99,"userName":"兜底审批人"}]}}"""
            },
            new CfStageDefinition { FID = 61, FFlowVersionId = 501, FStageKey = "done", FStageName = "归档", FType = "human", FSortOrder = 2 });
        db.Set<CfStageRouteRule>().Add(
            new CfStageRouteRule { FFlowVersionId = 501, FEdgeKey = "to_done", FFromStageKey = "approve", FToStageKey = "done", FRouteName = "归档", FPriority = 99, FIsDefault = true, FStatus = "active" });
        await db.SaveChangesAsync();

        var result = await CreateService(db).PreviewDraftVersionAsync(500, new CardFlowPathPreviewRequest
        {
            DataJson = "{}",
            OrgId = 50
        });

        // 推演继续到终点：两步都在
        Assert.Equal(new[] { "approve", "done" }, result.Steps.Select(s => s.StageKey));

        // 首步：处理人兜底 + 失败标注 assigneeUnresolved
        var first = result.Steps[0];
        Assert.NotNull(first.Approver);
        Assert.Equal("兜底审批人", Assert.Single(first.Approver!.ApproverNames));
        Assert.NotNull(first.Failure);
        Assert.Equal("assigneeUnresolved", first.Failure!.Kind);
        Assert.True(first.Failure.FallbackApplied);
        Assert.False(string.IsNullOrWhiteSpace(first.Failure.Message));

        // 终点节点无失败标注
        Assert.Null(result.Steps[1].Failure);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PreviewDraftVersion_无匹配分支且无兜底_标注noBranchMatch并停在终点()
    {
        using var db = TestDbContextFactory.Create(nameof(PreviewDraftVersion_无匹配分支且无兜底_标注noBranchMatch并停在终点));

        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 510, FFlowName = "无路可走", FFlowCode = "NBM", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 511, FFlowDefinitionId = 510, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FID = 70, FFlowVersionId = 511, FStageKey = "s0", FStageName = "起", FType = "human", FSortOrder = 1 },
            new CfStageDefinition { FID = 71, FFlowVersionId = 511, FStageKey = "target", FStageName = "目标", FType = "human", FSortOrder = 2 });
        db.Set<CfStageRouteRule>().Add(
            // 唯一出边有条件且不命中，且非默认、无默认兜底 → 无路可走
            new CfStageRouteRule { FFlowVersionId = 511, FEdgeKey = "cond_only", FFromStageKey = "s0", FToStageKey = "target", FRouteName = "仅条件", FConditionJson = """{"field":"card.amount","operator":"gte","value":5000}""", FPriority = 1, FStatus = "active" });
        await db.SaveChangesAsync();

        var result = await CreateService(db).PreviewDraftVersionAsync(510, new CardFlowPathPreviewRequest { DataJson = """{"amount":10}""" });

        var last = Assert.Single(result.Steps);
        Assert.Equal("s0", last.StageKey);
        Assert.NotNull(last.Failure);
        Assert.Equal("noBranchMatch", last.Failure!.Kind);
        Assert.False(last.Failure.FallbackApplied);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PreviewDraftVersion_自动节点未配插件_标注autoStageError并继续推演()
    {
        using var db = TestDbContextFactory.Create(nameof(PreviewDraftVersion_自动节点未配插件_标注autoStageError并继续推演));

        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 520, FFlowName = "自动节点未配", FFlowCode = "ASE", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 521, FFlowDefinitionId = 520, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now });
        db.Set<CfStageDefinition>().AddRange(
            // auto 节点 F插件注册ID 为空 → 静态可预判：运行时无法执行
            new CfStageDefinition { FID = 80, FFlowVersionId = 521, FStageKey = "autogen", FStageName = "自动生成凭证", FType = "auto", FSortOrder = 1, F插件注册ID = null },
            new CfStageDefinition { FID = 81, FFlowVersionId = 521, FStageKey = "end", FStageName = "结束", FType = "human", FSortOrder = 2 });
        db.Set<CfStageRouteRule>().Add(
            new CfStageRouteRule { FFlowVersionId = 521, FEdgeKey = "auto_to_end", FFromStageKey = "autogen", FToStageKey = "end", FRouteName = "继续", FPriority = 99, FIsDefault = true, FStatus = "active" });
        await db.SaveChangesAsync();

        var result = await CreateService(db).PreviewDraftVersionAsync(520, new CardFlowPathPreviewRequest { DataJson = "{}" });

        Assert.Equal(new[] { "autogen", "end" }, result.Steps.Select(s => s.StageKey));
        var auto = result.Steps[0];
        Assert.NotNull(auto.Failure);
        Assert.Equal("autoStageError", auto.Failure!.Kind);
        Assert.False(auto.Failure.FallbackApplied);
    }
}
