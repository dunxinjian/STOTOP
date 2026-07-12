using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class AssigneeStrategyNormalizationTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task SaveDraftVersion_PreservesSuperiorChainStrategyCasing()
    {
        using var db = TestDbContextFactory.Create(nameof(SaveDraftVersion_PreservesSuperiorChainStrategyCasing));
        SeedDraft(db);
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var detail = await service.SaveDraftVersionAsync(100, new SaveDraftVersionRequest
        {
            Stages =
            {
                new StageDefinitionRequest
                {
                    StageKey = "manager", Name = "主管审批", SortOrder = 1, Type = "human",
                    AssigneeStrategy = "superiorChain", AssigneeConfigJson = """{"maxLevels":3}"""
                }
            }
        }, operatorId: 1);

        Assert.Equal("superiorChain", detail.Stages[0].AssigneeStrategy);
        var reloaded = await service.GetVersionDetailAsync(100, detail.Id);
        Assert.Equal("superiorChain", reloaded!.Stages[0].AssigneeStrategy);
    }

    // 顺带修：既有 orgChain 保存后被强制小写为 "orgchain" → resolver 只认 "orgChain" 端到端失效。回归钉死。
    [Fact]
    public async global::System.Threading.Tasks.Task SaveDraftVersion_PreservesOrgChainStrategyCasing()
    {
        using var db = TestDbContextFactory.Create(nameof(SaveDraftVersion_PreservesOrgChainStrategyCasing));
        SeedDraft(db);
        await db.SaveChangesAsync();

        var service = new FlowDefinitionService(db, NullLogger<FlowDefinitionService>.Instance);
        var detail = await service.SaveDraftVersionAsync(100, new SaveDraftVersionRequest
        {
            Stages =
            {
                new StageDefinitionRequest
                {
                    StageKey = "manager", Name = "主管审批", SortOrder = 1, Type = "human",
                    AssigneeStrategy = "orgChain", AssigneeConfigJson = """{"maxLevels":20}"""
                }
            }
        }, operatorId: 1);

        Assert.Equal("orgChain", detail.Stages[0].AssigneeStrategy);
        var reloaded = await service.GetVersionDetailAsync(100, detail.Id);
        Assert.Equal("orgChain", reloaded!.Stages[0].AssigneeStrategy);
    }

    private static void SeedDraft(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 100, FFlowName = "费用报销", FFlowCode = "FYBS", FStatus = "draft", FOrgId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 200, FFlowDefinitionId = 100, FVersionNumber = 1, FStatus = "draft", FCreatedTime = DateTime.Now
        });
    }
}
