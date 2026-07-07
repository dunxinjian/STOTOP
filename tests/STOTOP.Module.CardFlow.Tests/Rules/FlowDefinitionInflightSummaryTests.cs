using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class FlowDefinitionInflightSummaryTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task 在途计数_按版本分组并收集被卡节点Key()
    {
        using var db = TestDbContextFactory.Create(nameof(在途计数_按版本分组并收集被卡节点Key));

        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 100, FFlowName = "费用报销", FFlowCode = "FYBS",
            FStatus = "published", FOrgId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 201, FFlowDefinitionId = 100, FVersionNumber = 1,
            FStatus = "published", FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 202, FFlowDefinitionId = 100, FVersionNumber = 2,
            FStatus = "published", FIsCurrentVersion = true, FCreatedTime = DateTime.Now
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 301, FFlowVersionId = 201, FStageKey = "manager", FStageName = "主管审批", FSortOrder = 1
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 302, FFlowVersionId = 202, FStageKey = "finance", FStageName = "财务复核", FSortOrder = 1
        });

        // v1 两张在途卡（同一节点 manager），v2 一张在途卡（finance）
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 401, FCardId = 501, FStageDefinitionId = 301, FStageName = "主管审批", FStatus = "active"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 402, FCardId = 502, FStageDefinitionId = 301, FStageName = "主管审批", FStatus = "active"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 403, FCardId = 503, FStageDefinitionId = 302, FStageName = "财务复核", FStatus = "active"
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 501, FFlowDefinitionId = 100, FFlowVersionId = 201, FStatus = "active",
            FCurrentStageInstanceId = 401, FOrgId = 1, FTenantId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 502, FFlowDefinitionId = 100, FFlowVersionId = 201, FStatus = "active",
            FCurrentStageInstanceId = 402, FOrgId = 1, FTenantId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 503, FFlowDefinitionId = 100, FFlowVersionId = 202, FStatus = "active",
            FCurrentStageInstanceId = 403, FOrgId = 1, FTenantId = 1, FCreatedTime = DateTime.Now
        });
        // 干扰项：终态卡与他定义的在途卡都不计
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 504, FFlowDefinitionId = 100, FFlowVersionId = 202, FStatus = "completed",
            FOrgId = 1, FTenantId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 505, FFlowDefinitionId = 999, FFlowVersionId = 202, FStatus = "active",
            FOrgId = 1, FTenantId = 1, FCreatedTime = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var summary = await service.GetInflightSummaryAsync(100);

        Assert.Equal(3, summary.Total);
        Assert.Equal(2, summary.ByVersion.Count);

        var v1 = summary.ByVersion.Single(v => v.VersionId == 201);
        Assert.Equal(1, v1.VersionNumber);
        Assert.Equal(2, v1.Count);
        Assert.Equal(new[] { "manager" }, v1.StuckStageKeys);

        var v2 = summary.ByVersion.Single(v => v.VersionId == 202);
        Assert.Equal(2, v2.VersionNumber);
        Assert.Equal(1, v2.Count);
        Assert.Equal(new[] { "finance" }, v2.StuckStageKeys);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 在途计数_退回状态卡片也计入()
    {
        using var db = TestDbContextFactory.Create(nameof(在途计数_退回状态卡片也计入));

        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 100, FFlowName = "费用报销", FFlowCode = "FYBS",
            FStatus = "published", FOrgId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 201, FFlowDefinitionId = 100, FVersionNumber = 1,
            FStatus = "published", FIsCurrentVersion = true, FCreatedTime = DateTime.Now
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 501, FFlowDefinitionId = 100, FFlowVersionId = 201, FStatus = "returned",
            FOrgId = 1, FTenantId = 1, FCreatedTime = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var summary = await service.GetInflightSummaryAsync(100);

        Assert.Equal(1, summary.Total);
        var v1 = Assert.Single(summary.ByVersion);
        Assert.Equal(1, v1.Count);
        Assert.Empty(v1.StuckStageKeys); // returned 卡无 active 实例
    }
}
