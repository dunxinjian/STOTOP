using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class StageConfigParserTests
{
    [Fact]
    public void ParseLegacyArray_ReturnsInputFields()
    {
        var parser = new StageConfigParser();

        var result = parser.Parse(@"[""accountCode"", ""internalAuditNote""]");

        Assert.Equal(1, result.Version);
        Assert.Equal(new[] { "accountCode", "internalAuditNote" }, result.InputFields);
        Assert.Null(result.ViewProfile);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ParseV2Envelope_ReturnsViewProfileActionPolicyApprovalModeAndInputFields()
    {
        var parser = new StageConfigParser();

        var result = parser.Parse("""
        {
          "version": 2,
          "inputFields": ["accountCode"],
          "viewProfile": {
            "profileName": "财务复核视图",
            "fieldAccess": {
              "accountCode": { "access": "required" }
            },
            "detailAccess": {
              "expenseDetails.invoiceNo": { "access": "readonly" }
            },
            "actions": ["approve", "reject"]
          },
          "actionPolicy": {
            "allowedActions": ["approve", "reject"]
          },
          "approvalMode": {
            "mode": "sequential"
          }
        }
        """);

        Assert.Equal(2, result.Version);
        Assert.Equal(new[] { "accountCode" }, result.InputFields);
        Assert.NotNull(result.ViewProfile);
        Assert.Equal("财务复核视图", result.ViewProfile!.ProfileName);
        Assert.Equal("required", result.ViewProfile.FieldAccess["accountCode"].Access);
        Assert.Equal("readonly", result.ViewProfile.DetailAccess["expenseDetails.invoiceNo"].Access);
        Assert.Equal(new[] { "approve", "reject" }, result.ActionPolicy!.AllowedActions);
        Assert.Equal("sequential", result.ApprovalMode!.Mode);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ParseInvalidJson_ReturnsEmptyEnvelopeWithoutThrowing()
    {
        var parser = new StageConfigParser();

        var result = parser.Parse("{not-json");

        Assert.Equal(1, result.Version);
        Assert.Empty(result.InputFields);
        Assert.NotEmpty(result.Warnings);
    }
}
