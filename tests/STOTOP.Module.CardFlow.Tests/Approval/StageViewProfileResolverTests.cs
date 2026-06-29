using System.Text.Json;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Models.Schema;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class StageViewProfileResolverTests
{
    [Fact]
    public void ResolveLegacyFallback_UsesInputFieldsAndDefaultActions()
    {
        var resolver = new StageViewProfileResolver();
        var card = new CfCard { FDataJson = """{"accountCode":"1001","amount":88}""" };
        var config = new StageConfigEnvelope
        {
            Version = 1,
            InputFields = new List<string> { "accountCode" }
        };

        var result = resolver.Resolve(
            """[{"key":"accountCode","label":"科目"},{"key":"amount","label":"金额"}]""",
            null,
            new CfStageDefinition { FStageName = "财务复核" },
            card,
            new List<CfCardDetail>(),
            operatorId: 1,
            config);

        Assert.Equal("editable", result.FieldAccess["accountCode"].Access);
        Assert.Equal("readonly", result.FieldAccess["amount"].Access);
        Assert.Contains("approve", result.AllowedActions);
        Assert.Contains("addSignAfter", result.AllowedActions);
        Assert.DoesNotContain("addSignBefore", result.AllowedActions);
    }

    [Fact]
    public void ResolveV2Profile_RedactsHiddenAndMaskedDataAndIntersectsActions()
    {
        var resolver = new StageViewProfileResolver();
        var card = new CfCard
        {
            FDataJson = """{"amount":88,"internalAuditNote":"secret","phone":"13800138000"}"""
        };
        var details = new List<CfCardDetail>
        {
            new()
            {
                FSortOrder = 0,
                FDataJson = """{"invoiceNo":"INV-1","accountSubject":"6601"}"""
            }
        };
        var config = new StageConfigEnvelope
        {
            Version = 2,
            ViewProfile = new StageViewProfile
            {
                FieldAccess = new Dictionary<string, StageFieldAccessRule>
                {
                    ["amount"] = new() { Access = "readonly" },
                    ["internalAuditNote"] = new() { Access = "hidden" },
                    ["phone"] = new() { Access = "masked" }
                },
                DetailAccess = new Dictionary<string, StageDetailAccessRule>
                {
                    ["default.invoiceNo"] = new() { Access = "hidden" },
                    ["default.accountSubject"] = new() { Access = "required" }
                },
                Actions = new List<string> { "approve", "transfer" }
            },
            ActionPolicy = new StageActionPolicy
            {
                AllowedActions = new List<string> { "approve", "reject" }
            }
        };

        var result = resolver.Resolve(
            """[{"key":"amount"},{"key":"internalAuditNote"},{"key":"phone"}]""",
            """[{"key":"invoiceNo"},{"key":"accountSubject"}]""",
            new CfStageDefinition { FStageName = "财务复核" },
            card,
            details,
            operatorId: 1,
            config);

        Assert.Equal(new[] { "approve" }, result.AllowedActions);

        using var cardJson = JsonDocument.Parse(result.RedactedDataJson);
        Assert.True(cardJson.RootElement.TryGetProperty("amount", out _));
        Assert.False(cardJson.RootElement.TryGetProperty("internalAuditNote", out _));
        Assert.NotEqual("13800138000", cardJson.RootElement.GetProperty("phone").GetString());

        using var detailJson = JsonDocument.Parse(result.RedactedDetails.Single().DataJson);
        Assert.False(detailJson.RootElement.TryGetProperty("invoiceNo", out _));
        Assert.Equal("6601", detailJson.RootElement.GetProperty("accountSubject").GetString());
    }

    [Fact]
    public void ResolveLegacyFallback_ObjectSchema_ExtractsFieldKeys()
    {
        var resolver = new StageViewProfileResolver();
        var card = new CfCard { FDataJson = """{"accountCode":"1001","amount":88}""" };
        var config = new StageConfigEnvelope
        {
            Version = 1,
            InputFields = new List<string> { "accountCode" }
        };

        var result = resolver.Resolve(
            """{"version":2,"fields":[{"key":"accountCode","label":"科目"},{"key":"amount","label":"金额"}]}""",
            null,
            new CfStageDefinition { FStageName = "财务复核" },
            card,
            new List<CfCardDetail>(),
            operatorId: 1,
            config);

        // 修复前：ReadFieldKeys 对 Object 形态返回空 → FieldAccess 不含这些键（取值抛 KeyNotFound）
        Assert.Equal("editable", result.FieldAccess["accountCode"].Access);
        Assert.Equal("readonly", result.FieldAccess["amount"].Access);
    }

    [Fact]
    public void Resolve_SensitiveFieldNotGrantedByNode_DefaultsToMaskedInWorkView()
    {
        var resolver = new StageViewProfileResolver();
        var card = new CfCard { FDataJson = """{"amount":88,"payeeAccountNo":"6222021234567890123"}""" };
        var config = new StageConfigEnvelope { Version = 2, InputFields = new List<string> { "amount" } };
        var result = resolver.Resolve(
            """[{"key":"amount","type":"money"},{"key":"payeeAccountNo","type":"text","sensitive":true,"maskPattern":"bankCard"}]""",
            null,
            new CfStageDefinition { FStageName = "审批" },
            card,
            new List<CfCardDetail>(),
            operatorId: 1,
            config);
        Assert.Equal("editable", result.FieldAccess["amount"].Access);          // 节点收集字段
        Assert.Equal("masked", result.FieldAccess["payeeAccountNo"].Access);    // 敏感且未授予 → 工作视图也 masked
        using var doc = global::System.Text.Json.JsonDocument.Parse(result.RedactedDataJson);
        Assert.NotEqual("6222021234567890123", doc.RootElement.GetProperty("payeeAccountNo").GetString());
    }
}
