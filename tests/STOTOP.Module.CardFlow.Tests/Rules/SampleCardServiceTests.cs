using System.Text.Json;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

/// <summary>M5-1 样例卡片：keyword 过滤 + 敏感脱敏（路由引用字段保留原值——精确差集脱敏）。</summary>
public class SampleCardServiceTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task GetSampleCards_按keyword过滤标题_只返回命中卡片()
    {
        using var db = TestDbContextFactory.Create(nameof(GetSampleCards_按keyword过滤标题_只返回命中卡片));
        SeedFlow(db);
        db.Set<CfCard>().AddRange(
            NewCard(1001, "差旅报销-北京", """{"amount":500,"idCard":"110101199001011234"}"""),
            NewCard(1002, "差旅报销-上海", """{"amount":800,"idCard":"310101199001011234"}"""),
            NewCard(1003, "办公用品采购", """{"amount":300,"idCard":"440101199001011234"}"""));
        await db.SaveChangesAsync();

        var service = new SampleCardService(db);
        var hit = await service.GetSampleCardsAsync(700, "差旅");

        Assert.Equal(2, hit.Count);
        Assert.All(hit, card => Assert.Contains("差旅", card.Title));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task GetSampleCards_敏感字段脱敏但路由引用字段保留原值()
    {
        using var db = TestDbContextFactory.Create(nameof(GetSampleCards_敏感字段脱敏但路由引用字段保留原值));

        // schema：amount 与 idCard 均标敏感；路由条件引用 card.amount → amount 保留、idCard 脱敏
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 700, FFlowName = "费用", FFlowCode = "FY", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 701, FFlowDefinitionId = 700, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now,
            FCardSchemaJson = """{"fields":[{"key":"amount","label":"金额","type":"money","sensitive":true},{"key":"idCard","label":"身份证","type":"text","sensitive":true}]}"""
        });
        db.Set<CfStageRouteRule>().Add(new CfStageRouteRule
        {
            FFlowVersionId = 701, FEdgeKey = "big", FFromStageKey = "s0", FToStageKey = "s1", FRouteName = "大额",
            FConditionJson = """{"field":"card.amount","operator":"gte","value":500}""", FPriority = 1, FStatus = "active"
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 1100, FFlowDefinitionId = 700, FFlowVersionId = 701, FTitle = "报销单",
            FOrgId = 1, FCreatedTime = DateTime.Now,
            FDataJson = """{"amount":800,"idCard":"110101199001011234"}"""
        });
        await db.SaveChangesAsync();

        var service = new SampleCardService(db);
        var cards = await service.GetSampleCardsAsync(700, null);

        var card = Assert.Single(cards);
        using var doc = JsonDocument.Parse(card.DataJson);
        // 路由引用的敏感字段 amount 保留原值
        Assert.Equal(800, doc.RootElement.GetProperty("amount").GetInt32());
        // 未被路由引用的敏感字段 idCard 已脱敏
        Assert.Equal("***", doc.RootElement.GetProperty("idCard").GetString());
    }

    private static CfCard NewCard(long id, string title, string dataJson) => new()
    {
        FID = id,
        FFlowDefinitionId = 700,
        FFlowVersionId = 701,
        FTitle = title,
        FOrgId = 1,
        FCreatedTime = DateTime.Now,
        FDataJson = dataJson
    };

    // 供 keyword 用例的 schema：amount/idCard 敏感但无路由引用（脱敏与否不影响该用例断言）
    private static void SeedFlow(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FID = 700, FFlowName = "费用", FFlowCode = "FY", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 701, FFlowDefinitionId = 700, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now,
            FCardSchemaJson = """{"fields":[{"key":"amount","label":"金额","type":"money"},{"key":"idCard","label":"身份证","type":"text","sensitive":true}]}"""
        });
    }
}
