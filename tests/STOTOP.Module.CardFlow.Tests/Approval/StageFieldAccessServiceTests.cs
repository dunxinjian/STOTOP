using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Models.Schema;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class StageFieldAccessServiceTests
{
    [Fact]
    public void ValidateSupplement_AllowsEditableAndRejectsReadonly()
    {
        var service = new StageFieldAccessService();
        var config = new StageConfigEnvelope
        {
            ViewProfile = new StageViewProfile
            {
                FieldAccess =
                {
                    ["accountCode"] = new() { Access = "editable" },
                    ["managerNote"] = new() { Access = "readonly" }
                }
            }
        };

        Assert.True(service.ValidateSupplement(config, new Dictionary<string, object> { ["accountCode"] = "6602" }).Success);

        var denied = service.ValidateSupplement(config, new Dictionary<string, object> { ["managerNote"] = "x" });
        Assert.False(denied.Success);
        Assert.Contains("managerNote", denied.ErrorMessage);
    }

    [Fact]
    public void ValidateSupplement_AcceptsLegacyInputFields()
    {
        var service = new StageFieldAccessService();
        var config = new StageConfigEnvelope { InputFields = new List<string> { "financeOpinion" } };

        Assert.True(service.ValidateSupplement(config, new Dictionary<string, object> { ["financeOpinion"] = "ok" }).Success);
    }

    [Fact]
    public void ValidateRequiredFields_BlocksMissingRequiredEditableField()
    {
        var service = new StageFieldAccessService();
        var config = new StageConfigEnvelope
        {
            ViewProfile = new StageViewProfile
            {
                FieldAccess =
                {
                    ["voucherNo"] = new() { Access = "required", Required = true }
                }
            }
        };

        var missing = service.ValidateRequiredFields(config, new Dictionary<string, object?>(), null);
        Assert.False(missing.Success);
        Assert.Contains("voucherNo", missing.ErrorMessage);

        var supplied = service.ValidateRequiredFields(
            config,
            new Dictionary<string, object?>(),
            new Dictionary<string, object> { ["voucherNo"] = "PZ-1" });
        Assert.True(supplied.Success);
    }

    [Fact]
    public void ValidateDetailEdits_UsesDetailTableAndColumnAccess()
    {
        var service = new StageFieldAccessService();
        var config = new StageConfigEnvelope
        {
            ViewProfile = new StageViewProfile
            {
                DetailAccess =
                {
                    ["expense.accountCode"] = new() { Access = "editable" },
                    ["expense.internalAuditNote"] = new() { Access = "hidden" }
                }
            }
        };

        Assert.True(service.ValidateDetailEdits(config, new List<DetailRowEditRequest>
        {
            new() { DetailTableKey = "expense", Values = { ["accountCode"] = "6602" } }
        }).Success);

        var denied = service.ValidateDetailEdits(config, new List<DetailRowEditRequest>
        {
            new() { DetailTableKey = "expense", Values = { ["internalAuditNote"] = "x" } }
        });
        Assert.False(denied.Success);
        Assert.Contains("expense.internalAuditNote", denied.ErrorMessage);
    }

    [Fact]
    public void ValidateSupplement_RejectsFieldWhenBoundComponentIsReadonly()
    {
        var service = new StageFieldAccessService();
        var config = new StageConfigEnvelope
        {
            ViewProfile = new StageViewProfile
            {
                FieldAccess =
                {
                    ["amount"] = new() { Access = "editable" }
                },
                Components =
                {
                    new StageComponentRef
                    {
                        Id = "amount-card",
                        Binding = new CardComponentBinding
                        {
                            Source = "cardField",
                            FieldKey = "amount"
                        }
                    }
                },
                ComponentAccess =
                {
                    ["amount-card"] = new StageComponentAccessRule { Access = "readonly" }
                }
            }
        };

        var denied = service.ValidateSupplement(config, new Dictionary<string, object> { ["amount"] = 100 });

        Assert.False(denied.Success);
        Assert.Contains("amount-card", denied.ErrorMessage);
    }

    [Fact]
    public void ValidateDetailEdits_RejectsTableWhenBoundComponentIsReadonly()
    {
        var service = new StageFieldAccessService();
        var config = new StageConfigEnvelope
        {
            ViewProfile = new StageViewProfile
            {
                DetailAccess =
                {
                    ["expense.amount"] = new() { Access = "editable" }
                },
                Components =
                {
                    new StageComponentRef
                    {
                        Id = "expense-lines",
                        Binding = new CardComponentBinding
                        {
                            Source = "detailTable",
                            DetailTableKey = "expense"
                        }
                    }
                },
                ComponentAccess =
                {
                    ["expense-lines"] = new StageComponentAccessRule { Access = "readonly" }
                }
            }
        };

        var denied = service.ValidateDetailEdits(config, new List<DetailRowEditRequest>
        {
            new() { DetailTableKey = "expense", Values = { ["amount"] = 100 } }
        });

        Assert.False(denied.Success);
        Assert.Contains("expense-lines", denied.ErrorMessage);
    }
}
