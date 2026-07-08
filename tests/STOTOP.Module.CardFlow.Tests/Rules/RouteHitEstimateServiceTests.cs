using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class RouteHitEstimateServiceTests
{
    private const long DefinitionId = 700;

    [Fact]
    public async global::System.Threading.Tasks.Task 命中率试算_三卡两命中一缺字段_统计口径正确()
    {
        using var db = TestDbContextFactory.Create(nameof(命中率试算_三卡两命中一缺字段_统计口径正确));
        // 卡1：amount=8000 → 命中（≥5000）；卡2：amount=1200 → 有值不命中；卡3：无 amount 字段 → 缺值不命中
        SeedCard(db, 1, """{"amount":8000}""");
        SeedCard(db, 2, """{"amount":1200}""");
        SeedCard(db, 3, """{"memo":"缺金额"}""");
        await db.SaveChangesAsync();

        var service = new RouteHitEstimateService(db, new ConditionRuleEvaluator());
        var result = await service.EstimateAsync(DefinitionId, new RouteHitEstimateRequest
        {
            ConditionJson = """{"logic":"and","conditions":[{"field":"amount","operator":"gte","value":5000}]}"""
        });

        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.WithValue);
        Assert.Equal(1, result.Hit);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 命中率试算_无历史卡片_返回全零()
    {
        using var db = TestDbContextFactory.Create(nameof(命中率试算_无历史卡片_返回全零));

        var service = new RouteHitEstimateService(db, new ConditionRuleEvaluator());
        var result = await service.EstimateAsync(DefinitionId, new RouteHitEstimateRequest
        {
            ConditionJson = """{"field":"amount","operator":"gte","value":5000}"""
        });

        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.WithValue);
        Assert.Equal(0, result.Hit);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 命中率试算_超过30天与其他流程的卡片_不计入采样()
    {
        using var db = TestDbContextFactory.Create(nameof(命中率试算_超过30天与其他流程的卡片_不计入采样));
        SeedCard(db, 1, """{"amount":8000}""");
        SeedCard(db, 2, """{"amount":9000}""", createdDaysAgo: 45);
        SeedCard(db, 3, """{"amount":9000}""", definitionId: 999);
        await db.SaveChangesAsync();

        var service = new RouteHitEstimateService(db, new ConditionRuleEvaluator());
        var result = await service.EstimateAsync(DefinitionId, new RouteHitEstimateRequest
        {
            ConditionJson = """{"field":"amount","operator":"gte","value":5000}"""
        });

        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.WithValue);
        Assert.Equal(1, result.Hit);
    }

    private static void SeedCard(
        STOTOP.Infrastructure.Data.STOTOPDbContext db,
        long id,
        string dataJson,
        int createdDaysAgo = 1,
        long definitionId = DefinitionId)
    {
        db.Set<CfCard>().Add(new CfCard
        {
            FID = id,
            FFlowDefinitionId = definitionId,
            FFlowVersionId = 1,
            FTitle = $"试算卡{id}",
            FStatus = "submitted",
            FInitiatorId = 9,
            FInitiatorName = "发起人",
            FCreatedTime = DateTime.Now.AddDays(-createdDaysAgo),
            FDataJson = dataJson,
            FCurrentRound = 1,
            FOrgId = 1,
            FTenantId = 1
        });
    }
}
