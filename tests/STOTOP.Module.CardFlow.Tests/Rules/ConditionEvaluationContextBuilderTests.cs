using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class ConditionEvaluationContextBuilderTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task BuildAsync_PopulatesInitiatorOrgIdAndDetailSummary()
    {
        using var db = TestDbContextFactory.Create(nameof(BuildAsync_PopulatesInitiatorOrgIdAndDetailSummary));
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 500,
            FOrgId = 88,
            FInitiatorId = 7,
            FInitiatorName = "张三",
            FSourceType = "expenseReimburse",
            FCurrentRound = 1,
            FDataJson = """{"amount":1000}"""
        });
        db.Set<CfCardDetail>().AddRange(
            new CfCardDetail { FID = 1, FCardId = 500, FDataJson = """{"amount":120,"tax":7}""" },
            new CfCardDetail { FID = 2, FCardId = 500, FDataJson = """{"amount":80}""" });
        await db.SaveChangesAsync();

        var builder = new ConditionEvaluationContextBuilder(db);
        var context = await builder.BuildAsync(db.Set<CfCard>().Find(500L)!);

        Assert.Equal(88L, context.InitiatorOrg["id"]);
        Assert.False(context.InitiatorOrg.ContainsKey("orgId"));
        Assert.Equal(2, context.DetailSummary["rowCount"]);
        Assert.Equal(200m, context.DetailSummary["amount"]);
        Assert.Equal(7m, context.DetailSummary["tax"]);
        Assert.Equal("expenseReimburse", context.SourceContext["sourceType"]);
        Assert.Equal(7L, context.Initiator["id"]);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task BuildAsync_WithCurrentStage_AddsCompletedStageKey()
    {
        using var db = TestDbContextFactory.Create(nameof(BuildAsync_WithCurrentStage_AddsCompletedStageKey));
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 501,
            FOrgId = 1,
            FInitiatorId = 1,
            FCurrentRound = 3,
            FDataJson = "{}"
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 90,
            FFlowVersionId = 1,
            FStageKey = "manager",
            FStageName = "主管",
            FType = "human"
        });
        await db.SaveChangesAsync();

        var builder = new ConditionEvaluationContextBuilder(db);
        var context = await builder.BuildAsync(
            db.Set<CfCard>().Find(501L)!,
            new CfStageInstance { FID = 11, FStageDefinitionId = 90, FFinalAction = "approve" });

        Assert.Equal(3, context.CurrentStageResult["round"]);
        Assert.Equal(11L, context.CurrentStageResult["stageInstanceId"]);
        Assert.Equal("approve", context.CurrentStageResult["action"]);
        Assert.Equal("manager", context.CurrentStageResult["completedStageKey"]);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task BuildAsync_ResolvesOrgChainAndRoles()
    {
        using var db = TestDbContextFactory.Create(nameof(BuildAsync_ResolvesOrgChainAndRoles));
        db.Set<SysOrganization>().AddRange(
            new SysOrganization { FID = 10, FParentId = 20, FCode = "LEAF", FName = "叶组织", FStatus = 1 },
            new SysOrganization { FID = 20, FParentId = 0, FCode = "ROOT", FName = "根组织", FStatus = 1 });
        db.Set<SysRole>().Add(new SysRole { FID = 100, FCode = "FIN", FName = "财务", FStatus = 1 });
        db.Set<SysUserRole>().Add(new SysUserRole { FID = 1, FUserId = 7, FRoleId = 100 });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 600, FOrgId = 10, FInitiatorId = 7, FCurrentRound = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();

        var builder = new ConditionEvaluationContextBuilder(db);
        var context = await builder.BuildAsync(db.Set<CfCard>().Find(600L)!);

        Assert.Contains("10", context.OrgChain);
        Assert.Contains("20", context.OrgChain);   // 上溯到父组织
        Assert.Contains("FIN", context.RoleCodes);
        Assert.Contains("财务", context.RoleNames);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task BuildAsync_ExcludesDisabledRoles()
    {
        using var db = TestDbContextFactory.Create(nameof(BuildAsync_ExcludesDisabledRoles));
        db.Set<SysRole>().AddRange(
            new SysRole { FID = 200, FCode = "ACTIVE", FName = "在用角色", FStatus = 1 },
            new SysRole { FID = 201, FCode = "DISABLED", FName = "停用角色", FStatus = 0 });
        db.Set<SysUserRole>().AddRange(
            new SysUserRole { FID = 10, FUserId = 8, FRoleId = 200 },
            new SysUserRole { FID = 11, FUserId = 8, FRoleId = 201 });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 610, FOrgId = 1, FInitiatorId = 8, FCurrentRound = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();

        var builder = new ConditionEvaluationContextBuilder(db);
        var context = await builder.BuildAsync(db.Set<CfCard>().Find(610L)!);

        Assert.Contains("ACTIVE", context.RoleCodes);
        Assert.DoesNotContain("DISABLED", context.RoleCodes);   // 停用角色不参与路由
        Assert.DoesNotContain("停用角色", context.RoleNames);
    }
}
