using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class CardFlowPathPreviewServiceTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task PreviewDraftVersion_FollowsConditionalRouteAndDefaultBranch()
    {
        using var db = TestDbContextFactory.Create(nameof(PreviewDraftVersion_FollowsConditionalRouteAndDefaultBranch));
        SeedFlow(db);
        await db.SaveChangesAsync();

        var service = new CardFlowPathPreviewService(
            db,
            new ConditionRuleEvaluator(),
            new AuditSnapshotPolicyService());

        var large = await service.PreviewDraftVersionAsync(100, new CardFlowPathPreviewRequest
        {
            DataJson = """{"amount":6800}"""
        });
        var small = await service.PreviewDraftVersionAsync(100, new CardFlowPathPreviewRequest
        {
            DataJson = """{"amount":1200}"""
        });

        Assert.Equal(new[] { "manager", "gm", "finance" }, large.Steps.Select(step => step.StageKey));
        Assert.Equal("large_amount", large.Steps[0].SelectedEdgeKey);
        Assert.Equal(new[] { "manager", "finance" }, small.Steps.Select(step => step.StageKey));
        Assert.Equal("default_finance", small.Steps[0].SelectedEdgeKey);
    }

    private static void SeedFlow(STOTOP.Infrastructure.Data.STOTOPDbContext db)
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
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FID = 1, FFlowVersionId = 200, FStageKey = "manager", FStageName = "主管审批", FType = "human", FSortOrder = 1 },
            new CfStageDefinition { FID = 2, FFlowVersionId = 200, FStageKey = "finance", FStageName = "财务复核", FType = "human", FSortOrder = 2 },
            new CfStageDefinition { FID = 3, FFlowVersionId = 200, FStageKey = "gm", FStageName = "总经理审批", FType = "human", FSortOrder = 3 });
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule
            {
                FFlowVersionId = 200,
                FEdgeKey = "large_amount",
                FFromStageKey = "manager",
                FToStageKey = "gm",
                FRouteName = "大额报销",
                FConditionJson = """{"field":"card.amount","operator":"gte","value":5000}""",
                FPriority = 1,
                FStatus = "active"
            },
            new CfStageRouteRule
            {
                FFlowVersionId = 200,
                FEdgeKey = "default_finance",
                FFromStageKey = "manager",
                FToStageKey = "finance",
                FRouteName = "其他情况",
                FPriority = 99,
                FIsDefault = true,
                FStatus = "active"
            },
            new CfStageRouteRule
            {
                FFlowVersionId = 200,
                FEdgeKey = "gm_to_finance",
                FFromStageKey = "gm",
                FToStageKey = "finance",
                FRouteName = "总经理后财务",
                FPriority = 99,
                FIsDefault = true,
                FStatus = "active"
            });
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PreviewDraftVersion_OrgRouting_UsesInitiatorOrgId()
    {
        using var db = TestDbContextFactory.Create(nameof(PreviewDraftVersion_OrgRouting_UsesInitiatorOrgId));
        SeedOrgRoutingFlow(db);
        await db.SaveChangesAsync();

        var service = new CardFlowPathPreviewService(db, new ConditionRuleEvaluator(), new AuditSnapshotPolicyService());

        var hit = await service.PreviewDraftVersionAsync(300, new CardFlowPathPreviewRequest
        {
            DataJson = "{}",
            OrgId = 9
        });

        // InitiatorOrg 键收敛为 id 后，initiatorOrg.id == 9 命中 vip 分支
        Assert.Equal("vip_org", hit.Steps[0].SelectedEdgeKey);
        Assert.Equal("vip", hit.Steps[1].StageKey);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PreviewDraftVersion_BuildsDetailSummaryWithZeroRowCount()
    {
        using var db = TestDbContextFactory.Create(nameof(PreviewDraftVersion_BuildsDetailSummaryWithZeroRowCount));
        SeedDetailSummaryFlow(db);
        await db.SaveChangesAsync();

        var service = new CardFlowPathPreviewService(db, new ConditionRuleEvaluator(), new AuditSnapshotPolicyService());

        var result = await service.PreviewDraftVersionAsync(310, new CardFlowPathPreviewRequest
        {
            DataJson = "{}"
        });

        // 预演无明细 → DetailSummary 补建为 rowCount=0；detailSummary.rowCount eq 0 命中
        Assert.Equal("no_detail", result.Steps[0].SelectedEdgeKey);
    }

    private static void SeedOrgRoutingFlow(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 300, FFlowName = "组织路由", FFlowCode = "ORG", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 301, FFlowDefinitionId = 300, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FID = 10, FFlowVersionId = 301, FStageKey = "start", FStageName = "开始", FType = "human", FSortOrder = 1 },
            new CfStageDefinition { FID = 11, FFlowVersionId = 301, FStageKey = "vip", FStageName = "VIP", FType = "human", FSortOrder = 2 },
            new CfStageDefinition { FID = 12, FFlowVersionId = 301, FStageKey = "normal", FStageName = "普通", FType = "human", FSortOrder = 3 });
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule { FFlowVersionId = 301, FEdgeKey = "vip_org", FFromStageKey = "start", FToStageKey = "vip", FRouteName = "VIP组织", FConditionJson = """{"field":"initiatorOrg.id","operator":"eq","value":9}""", FPriority = 1, FStatus = "active" },
            new CfStageRouteRule { FFlowVersionId = 301, FEdgeKey = "normal_default", FFromStageKey = "start", FToStageKey = "normal", FRouteName = "其他", FPriority = 99, FIsDefault = true, FStatus = "active" });
        // vip / normal 无出边(终端)，预演到达即止，路径稳定
    }

    private static void SeedDetailSummaryFlow(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 310, FFlowName = "明细汇总", FFlowCode = "DS", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 311, FFlowDefinitionId = 310, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FID = 20, FFlowVersionId = 311, FStageKey = "begin", FStageName = "起", FType = "human", FSortOrder = 1 },
            new CfStageDefinition { FID = 21, FFlowVersionId = 311, FStageKey = "empty", FStageName = "无明细", FType = "human", FSortOrder = 2 },
            new CfStageDefinition { FID = 22, FFlowVersionId = 311, FStageKey = "other", FStageName = "其他", FType = "human", FSortOrder = 3 });
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule { FFlowVersionId = 311, FEdgeKey = "no_detail", FFromStageKey = "begin", FToStageKey = "empty", FRouteName = "无明细", FConditionJson = """{"field":"detailSummary.rowCount","operator":"eq","value":0}""", FPriority = 1, FStatus = "active" },
            new CfStageRouteRule { FFlowVersionId = 311, FEdgeKey = "ds_default", FFromStageKey = "begin", FToStageKey = "other", FRouteName = "其他", FPriority = 99, FIsDefault = true, FStatus = "active" });
        // empty / other 终端
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PreviewDraftVersion_EmptyConditionNonDefault_NotTreatedAsCatchAll()
    {
        using var db = TestDbContextFactory.Create(nameof(PreviewDraftVersion_EmptyConditionNonDefault_NotTreatedAsCatchAll));
        SeedEmptyConditionFlow(db);
        await db.SaveChangesAsync();

        var service = new CardFlowPathPreviewService(db, new ConditionRuleEvaluator(), new AuditSnapshotPolicyService());

        var result = await service.PreviewDraftVersionAsync(400, new CardFlowPathPreviewRequest { DataJson = "{}" });

        // 空条件非默认规则不再被当 catch-all；落默认分支
        Assert.Equal("to_default", result.Steps[0].SelectedEdgeKey);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PreviewDraftVersion_FollowsEdgeMatchedByDefinitionIdOnly()
    {
        using var db = TestDbContextFactory.Create(nameof(PreviewDraftVersion_FollowsEdgeMatchedByDefinitionIdOnly));
        SeedDefinitionIdEdgeFlow(db);
        await db.SaveChangesAsync();

        var service = new CardFlowPathPreviewService(db, new ConditionRuleEvaluator(), new AuditSnapshotPolicyService());

        var result = await service.PreviewDraftVersionAsync(410, new CardFlowPathPreviewRequest { DataJson = "{}" });

        // 仅靠 FFromStageDefinitionId 关联的边，预演也能跟随
        Assert.Equal(new[] { "n1", "n2" }, result.Steps.Select(step => step.StageKey));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PreviewDraftVersion_TypeErrorInOrGroup_StillSelected()
    {
        using var db = TestDbContextFactory.Create(nameof(PreviewDraftVersion_TypeErrorInOrGroup_StillSelected));
        SeedTypeErrorFlow(db);
        await db.SaveChangesAsync();

        var service = new CardFlowPathPreviewService(db, new ConditionRuleEvaluator(), new AuditSnapshotPolicyService());

        var result = await service.PreviewDraftVersionAsync(420, new CardFlowPathPreviewRequest
        {
            // amount 存在但非数值比较对象 → gt 叶产生 TypeError；flag 叶为真 → or 组整体命中
            DataJson = """{"flag":true,"amount":100}"""
        });

        // or 组含一个类型错子条件但整体真命中 → 与运行时一致地选中(不再因 TypeErrors 跳过)
        Assert.Equal("typed_hit", result.Steps[0].SelectedEdgeKey);
    }

    private static void SeedEmptyConditionFlow(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 400, FFlowName = "空条件", FFlowCode = "EC", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 401, FFlowDefinitionId = 400, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FID = 30, FFlowVersionId = 401, FStageKey = "s0", FStageName = "起", FType = "human", FSortOrder = 1 },
            new CfStageDefinition { FID = 31, FFlowVersionId = 401, FStageKey = "trap", FStageName = "陷阱", FType = "human", FSortOrder = 2 },
            new CfStageDefinition { FID = 32, FFlowVersionId = 401, FStageKey = "safe", FStageName = "默认", FType = "human", FSortOrder = 3 });
        db.Set<CfStageRouteRule>().AddRange(
            // 非默认规则，条件为空(null) —— 修复前被当 catch-all
            new CfStageRouteRule { FFlowVersionId = 401, FEdgeKey = "empty_trap", FFromStageKey = "s0", FToStageKey = "trap", FRouteName = "空条件", FConditionJson = null, FPriority = 1, FStatus = "active" },
            new CfStageRouteRule { FFlowVersionId = 401, FEdgeKey = "to_default", FFromStageKey = "s0", FToStageKey = "safe", FRouteName = "默认", FPriority = 99, FIsDefault = true, FStatus = "active" });
        // trap / safe 终端：修复后 s0→safe(to_default)，修复前 s0→trap(empty_trap)
    }

    private static void SeedDefinitionIdEdgeFlow(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 410, FFlowName = "DefId边", FFlowCode = "DID", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 411, FFlowDefinitionId = 410, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FID = 40, FFlowVersionId = 411, FStageKey = "n1", FStageName = "节点1", FType = "human", FSortOrder = 1 },
            new CfStageDefinition { FID = 41, FFlowVersionId = 411, FStageKey = "n2", FStageName = "节点2", FType = "human", FSortOrder = 2 });
        db.Set<CfStageRouteRule>().Add(
            // FFromStageKey 为空，仅靠 FFromStageDefinitionId=40 关联到 n1；n2 终端
            new CfStageRouteRule { FFlowVersionId = 411, FEdgeKey = "by_defid", FFromStageKey = "", FFromStageDefinitionId = 40, FToStageKey = "n2", FRouteName = "按DefId", FPriority = 99, FIsDefault = true, FStatus = "active" });
    }

    private static void SeedTypeErrorFlow(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 420, FFlowName = "类型错", FFlowCode = "TE", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 421, FFlowDefinitionId = 420, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FID = 50, FFlowVersionId = 421, FStageKey = "t0", FStageName = "起", FType = "human", FSortOrder = 1 },
            new CfStageDefinition { FID = 51, FFlowVersionId = 421, FStageKey = "hit", FStageName = "命中", FType = "human", FSortOrder = 2 },
            new CfStageDefinition { FID = 52, FFlowVersionId = 421, FStageKey = "miss", FStageName = "默认", FType = "human", FSortOrder = 3 });
        db.Set<CfStageRouteRule>().AddRange(
            // or 组：一个类型错叶(amount gt "x"，amount 存在但与字符串不可比) + 一个真叶(flag eq true) → Matched=true 且 TypeErrors 非空
            new CfStageRouteRule { FFlowVersionId = 421, FEdgeKey = "typed_hit", FFromStageKey = "t0", FToStageKey = "hit", FRouteName = "含类型错命中", FConditionJson = """{"logic":"or","conditions":[{"field":"card.amount","operator":"gt","value":"x"},{"field":"card.flag","operator":"eq","value":true}]}""", FPriority = 1, FStatus = "active" },
            new CfStageRouteRule { FFlowVersionId = 421, FEdgeKey = "te_default", FFromStageKey = "t0", FToStageKey = "miss", FRouteName = "默认", FPriority = 99, FIsDefault = true, FStatus = "active" });
        // hit / miss 终端
    }

    [Fact]
    public async global::System.Threading.Tasks.Task PreviewDraftVersion_InOrgChain_MatchesViaResolvedOrgChain()
    {
        using var db = TestDbContextFactory.Create(nameof(PreviewDraftVersion_InOrgChain_MatchesViaResolvedOrgChain));
        // 组织层级：叶 10 → 父 20
        db.Set<SysOrganization>().AddRange(
            new SysOrganization { FID = 10, FParentId = 20, FCode = "LEAF", FName = "叶", FStatus = 1 },
            new SysOrganization { FID = 20, FParentId = 0, FCode = "ROOT", FName = "根", FStatus = 1 });
        SeedInOrgChainFlow(db);
        await db.SaveChangesAsync();

        var service = new CardFlowPathPreviewService(db, new ConditionRuleEvaluator(), new AuditSnapshotPolicyService());

        // OrgId=10：org 链含祖先 20 → inOrgChain value=20 命中 vip 分支
        var hit = await service.PreviewDraftVersionAsync(900, new CardFlowPathPreviewRequest
        {
            DataJson = "{}",
            OrgId = 10
        });

        Assert.Equal("vip_org", hit.Steps[0].SelectedEdgeKey);
        Assert.Equal("vip", hit.Steps[1].StageKey);
    }

    private static void SeedInOrgChainFlow(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 900, FFlowName = "组织链路由", FFlowCode = "ORGCHAIN", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FID = 901, FFlowDefinitionId = 900, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FID = 90, FFlowVersionId = 901, FStageKey = "start", FStageName = "开始", FType = "human", FSortOrder = 1 },
            new CfStageDefinition { FID = 91, FFlowVersionId = 901, FStageKey = "vip", FStageName = "VIP", FType = "human", FSortOrder = 2 },
            new CfStageDefinition { FID = 92, FFlowVersionId = 901, FStageKey = "normal", FStageName = "普通", FType = "human", FSortOrder = 3 });
        db.Set<CfStageRouteRule>().AddRange(
            new CfStageRouteRule { FFlowVersionId = 901, FEdgeKey = "vip_org", FFromStageKey = "start", FToStageKey = "vip", FRouteName = "属于根组织链", FConditionJson = """{"field":"initiatorOrg.id","operator":"inOrgChain","value":20}""", FPriority = 1, FStatus = "active" },
            new CfStageRouteRule { FFlowVersionId = 901, FEdgeKey = "normal_default", FFromStageKey = "start", FToStageKey = "normal", FRouteName = "其他", FPriority = 99, FIsDefault = true, FStatus = "active" });
        // vip / normal 终端
    }
}
