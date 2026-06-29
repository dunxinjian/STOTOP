using STOTOP.Module.CardFlow.Models.Rules;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class AuditSnapshotPolicyServiceTests
{
    [Fact]
    public void BuildRouteDecisionSnapshotJson_AppliesNeverStoreAndMaskPolicies()
    {
        var service = new AuditSnapshotPolicyService();
        var context = new ConditionEvaluationContext
        {
            CardData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["amount"] = 6800m,
                ["bankAccount"] = "6222020202020202020",
                ["attachmentUrl"] = "https://example.test/private-file.pdf"
            }
        };
        var result = new StageRouteResolveResult
        {
            Reason = "命中条件",
            Candidates =
            {
                new StageRouteCandidateResult
                {
                    EdgeKey = "large_amount",
                    ConsumedFields = { "card.amount", "card.bankAccount", "card.attachmentUrl" }
                }
            }
        };

        var json = service.BuildRouteDecisionSnapshotJson(context, result);

        Assert.Contains("\"policy\":\"store\"", json);
        Assert.Contains("\"policy\":\"mask\"", json);
        Assert.Contains("\"policy\":\"neverStore\"", json);
        Assert.DoesNotContain("6222020202020202020", json);
        Assert.DoesNotContain("private-file.pdf", json);
    }
}
