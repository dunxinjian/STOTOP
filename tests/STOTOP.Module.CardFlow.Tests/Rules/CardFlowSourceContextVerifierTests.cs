using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.OA.Entities;
using System.Text.Json.Nodes;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class CardFlowSourceContextVerifierTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task VerifyAsync_LoadsTrustedExpenseRequestFactsInsteadOfTrustingInitialData()
    {
        using var db = TestDbContextFactory.Create(nameof(VerifyAsync_LoadsTrustedExpenseRequestFactsInsteadOfTrustingInitialData));
        db.Set<OaExpenseRequest>().Add(new OaExpenseRequest
        {
            FID = 10,
            FDocNumber = "QK20260609001",
            FApplicantId = 7,
            FDeptId = 8,
            FOrgId = 1,
            FReason = "差旅请款",
            FAmount = 1234.56m,
            FExpenseType = "差旅费",
            FDocStatus = 2,
            FCreatedTime = DateTime.Now
        });
        await db.SaveChangesAsync();

        var verifier = new CardFlowSourceContextVerifier(db);
        var result = await verifier.VerifyAsync(new CreateCardRequest
        {
            OrgId = 1,
            SourceModule = "oa",
            SourceType = "expense_request",
            SourceId = 10,
            InitialDataJson = """{"amount":999999,"expenseType":"伪造","freeText":"prefill-only"}"""
        });

        var trusted = JsonNode.Parse(result.TrustedDataJson!)!.AsObject();
        Assert.True(result.SourceVerified);
        Assert.Equal(1234.56m, trusted["amount"]!.GetValue<decimal>());
        Assert.Equal("差旅费", trusted["expenseType"]!.GetValue<string>());
        Assert.DoesNotContain("999999", result.TrustedDataJson);
        Assert.Contains("prefill-only", result.StoredInitialDataJson);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task VerifyAsync_DoesNotPromoteInitialDataWhenSourceHasNoProvider()
    {
        using var db = TestDbContextFactory.Create(nameof(VerifyAsync_DoesNotPromoteInitialDataWhenSourceHasNoProvider));

        var verifier = new CardFlowSourceContextVerifier(db);
        var result = await verifier.VerifyAsync(new CreateCardRequest
        {
            OrgId = 1,
            SourceModule = "unknown",
            SourceType = "custom",
            SourceId = 99,
            InitialDataJson = """{"amount":999999}"""
        });

        Assert.False(result.SourceVerified);
        Assert.Null(result.TrustedDataJson);
        Assert.Contains("没有可用的来源事实提供器", result.Warnings);
    }
}
