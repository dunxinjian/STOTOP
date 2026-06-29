using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Redaction;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class CardListInitialDataExposureTests
{
    private const string InitialSecret = "INITIAL-SECRET-9988";

    private static CardService BuildService(STOTOPDbContext db) => new(
        db,
        NullLogger<CardService>.Instance,
        new StageConfigParser(),
        new StageViewProfileResolver(new CardPresentationResolver()),
        new CardFlowSourceContextVerifier(db),
        new CardRedactionService());

    [Fact]
    public async global::System.Threading.Tasks.Task GetCardsAsync_DoesNotExposeInitialDataJson()
    {
        using var db = TestDbContextFactory.Create(nameof(GetCardsAsync_DoesNotExposeInitialDataJson));
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 1,
            FFlowName = "费用报销",
            FFlowCode = "EXP",
            FStatus = "published",
            FOrgId = 1,
            FCreatedTime = DateTime.Now
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 300,
            FFlowDefinitionId = 1,
            FFlowVersionId = 1,
            FTitle = "报销单",
            FStatus = "submitted",
            FInitiatorId = 9,
            FInitiatorName = "发起人",
            FDataJson = "{}",
            FInitialDataJson = """{"payeeAccountNo":"INITIAL-SECRET-9988"}""",
            FCurrentRound = 1,
            FOrgId = 1,
            FCreatedTime = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var result = await service.GetCardsAsync(new CardQueryRequest { OrgId = 1, Page = 1, PageSize = 10 });

        var item = Assert.Single(result.Items);
        Assert.Equal(300, item.Id);

        // 列表不得携带预填业务载荷——序列化后不出现原始值
        var serialized = JsonSerializer.Serialize(result.Items);
        Assert.DoesNotContain(InitialSecret, serialized);
    }
}
