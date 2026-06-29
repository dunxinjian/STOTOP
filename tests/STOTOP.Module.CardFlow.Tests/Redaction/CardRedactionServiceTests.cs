using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Models.Schema;
using STOTOP.Module.CardFlow.Services.Redaction;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Redaction;

public class CardRedactionServiceTests
{
    private const string Schema =
        """[{"key":"amount","type":"money"},{"key":"payeeAccountNo","type":"text","sensitive":true,"maskPattern":"bankCard"}]""";

    private static CfCard Card() => new()
    {
        FDataJson = """{"amount":88,"payeeAccountNo":"6222021234567890123","legacyNote":"x"}"""
    };

    [Fact]
    public void NoActiveNoGrant_SensitiveMasked_NonSensitiveReadonly()
    {
        var svc = new CardRedactionService();
        var result = svc.Redact(new CardRedactionRequest { Card = Card(), CardSchemaJson = Schema });

        Assert.Equal("readonly", result.FieldAccess["amount"].Access);
        Assert.Equal("masked", result.FieldAccess["payeeAccountNo"].Access);
        Assert.Equal("bankCard", result.FieldAccess["payeeAccountNo"].MaskPattern);
    }

    [Fact]
    public void StickyGrant_PastHandlerKeepsClearView()
    {
        var svc = new CardRedactionService();
        var handled = new StageConfigEnvelope
        {
            ViewProfile = new StageViewProfile
            {
                FieldAccess = new() { ["payeeAccountNo"] = new() { Access = "readonly" } }
            }
        };
        var result = svc.Redact(new CardRedactionRequest
        {
            Card = Card(),
            CardSchemaJson = Schema,
            HandledStageConfigs = new[] { handled }
        });

        Assert.Equal("readonly", result.FieldAccess["payeeAccountNo"].Access);
    }

    [Fact]
    public void ActiveAssignee_NodeConfigIsAuthoritative()
    {
        var svc = new CardRedactionService();
        var active = new StageConfigEnvelope
        {
            Version = 2,
            InputFields = new() { "amount" },
            ViewProfile = new StageViewProfile
            {
                FieldAccess = new() { ["payeeAccountNo"] = new() { Access = "editable" } }
            }
        };
        var result = svc.Redact(new CardRedactionRequest
        {
            Card = Card(),
            CardSchemaJson = Schema,
            ActiveStageConfig = active
        });

        Assert.Equal("editable", result.FieldAccess["amount"].Access);
        Assert.Equal("editable", result.FieldAccess["payeeAccountNo"].Access);
    }

    [Fact]
    public void ActiveAssignee_HiddenOverridesBaseline()
    {
        var svc = new CardRedactionService();
        var active = new StageConfigEnvelope
        {
            Version = 2,
            ViewProfile = new StageViewProfile
            {
                FieldAccess = new() { ["amount"] = new() { Access = "hidden" } }
            }
        };
        var result = svc.Redact(new CardRedactionRequest
        {
            Card = Card(),
            CardSchemaJson = Schema,
            ActiveStageConfig = active
        });

        Assert.Equal("hidden", result.FieldAccess["amount"].Access);
        Assert.Equal("masked", result.FieldAccess["payeeAccountNo"].Access);
    }

    [Fact]
    public void UnknownAccessString_OnActiveNode_FailsClosedToMasked()
    {
        var svc = new CardRedactionService();
        var active = new StageConfigEnvelope
        {
            Version = 2,
            ViewProfile = new StageViewProfile
            {
                // 配置笔误/枚举漂移：未知 access 串绝不能变明文
                FieldAccess = new() { ["payeeAccountNo"] = new() { Access = "redaonly" } }
            }
        };
        var result = svc.Redact(new CardRedactionRequest { Card = Card(), CardSchemaJson = Schema, ActiveStageConfig = active });
        Assert.Equal("masked", result.FieldAccess["payeeAccountNo"].Access);
    }

    [Fact]
    public void UnknownAccessString_OnHandledNode_DoesNotGrantStickyClearView()
    {
        var svc = new CardRedactionService();
        var handled = new StageConfigEnvelope
        {
            ViewProfile = new StageViewProfile
            {
                FieldAccess = new() { ["payeeAccountNo"] = new() { Access = "??" } }
            }
        };
        var result = svc.Redact(new CardRedactionRequest { Card = Card(), CardSchemaJson = Schema, HandledStageConfigs = new[] { handled } });
        // 未知串不构成提权 → 敏感字段保持 masked
        Assert.Equal("masked", result.FieldAccess["payeeAccountNo"].Access);
    }

    [Fact]
    public void StickyGrant_ViaInputFields_LiftsSensitiveToReadonly()
    {
        var svc = new CardRedactionService();
        var handled = new StageConfigEnvelope { InputFields = new() { "payeeAccountNo" } };
        var result = svc.Redact(new CardRedactionRequest { Card = Card(), CardSchemaJson = Schema, HandledStageConfigs = new[] { handled } });
        Assert.Equal("readonly", result.FieldAccess["payeeAccountNo"].Access);
    }

    [Fact]
    public void NullSchema_ProducesEmptyFieldAccess()
    {
        var svc = new CardRedactionService();
        var result = svc.Redact(new CardRedactionRequest { Card = Card(), CardSchemaJson = null });
        Assert.Empty(result.FieldAccess);
    }

    [Fact]
    public void Allowlist_DropsSchemaExternalKeys_AndMasksSensitive()
    {
        var svc = new CardRedactionService();
        var result = svc.Redact(new CardRedactionRequest { Card = Card(), CardSchemaJson = Schema });

        using var doc = global::System.Text.Json.JsonDocument.Parse(result.RedactedDataJson);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("amount", out _));        // schema 内非敏感 → 保留
        Assert.False(root.TryGetProperty("legacyNote", out _));   // schema 外 → 移除
        Assert.Equal("**** **** **** 0123",
            root.GetProperty("payeeAccountNo").GetString());      // 敏感 → bankCard 打码
    }

    [Fact]
    public void Allowlist_RedactsDetailRows()
    {
        var svc = new CardRedactionService();
        const string detailSchema = """[{"key":"invoiceNo","type":"text"},{"key":"idNo","type":"text","sensitive":true,"maskPattern":"idCard"}]""";
        var details = new List<CfCardDetail>
        {
            new() { FID = 1, FSortOrder = 0, FDetailTableKey = "default",
                    FDataJson = """{"invoiceNo":"INV-1","idNo":"110101199003075678","junk":"z"}""" }
        };
        var result = svc.Redact(new CardRedactionRequest
        {
            Card = Card(), CardSchemaJson = Schema, DetailSchemaJson = detailSchema, Details = details
        });

        using var doc = global::System.Text.Json.JsonDocument.Parse(result.RedactedDetails.Single().DataJson);
        var root = doc.RootElement;
        Assert.Equal("INV-1", root.GetProperty("invoiceNo").GetString());
        Assert.Equal("1101**********5678", root.GetProperty("idNo").GetString());
        Assert.False(root.TryGetProperty("junk", out _));         // schema 外明细列移除
    }

    [Fact]
    public void Detail_MultiTableSchema_RedactsPerTable()
    {
        var svc = new CardRedactionService();
        const string detailSchema =
            """{"version":2,"tables":[{"detailTableKey":"lines","columns":[{"key":"amt","type":"money"},{"key":"idNo","type":"text","sensitive":true,"maskPattern":"idCard"}]}]}""";
        var details = new List<CfCardDetail>
        {
            new() { FID = 1, FSortOrder = 0, FDetailTableKey = "lines",
                    FDataJson = """{"amt":100,"idNo":"110101199003075678","junk":"z"}""" }
        };
        var result = svc.Redact(new CardRedactionRequest
        {
            Card = Card(), CardSchemaJson = Schema, DetailSchemaJson = detailSchema, Details = details
        });

        using var doc = global::System.Text.Json.JsonDocument.Parse(result.RedactedDetails.Single().DataJson);
        var root = doc.RootElement;
        Assert.Equal("1101**********5678", root.GetProperty("idNo").GetString());  // 非default表也脱敏
        Assert.True(root.TryGetProperty("amt", out _));                            // 非敏感列保留
        Assert.False(root.TryGetProperty("junk", out _));                          // schema 外列移除
    }

    [Fact]
    public void HiddenField_OmittedFromOutput()
    {
        var svc = new CardRedactionService();
        var active = new StageConfigEnvelope
        {
            Version = 2,
            ViewProfile = new StageViewProfile { FieldAccess = new() { ["amount"] = new() { Access = "hidden" } } }
        };
        var result = svc.Redact(new CardRedactionRequest { Card = Card(), CardSchemaJson = Schema, ActiveStageConfig = active });

        using var doc = global::System.Text.Json.JsonDocument.Parse(result.RedactedDataJson);
        Assert.False(doc.RootElement.TryGetProperty("amount", out _));
    }

    [Fact]
    public void MaskedNumericField_FullyMasked()
    {
        var svc = new CardRedactionService();
        // amount 标敏感，数字 88 打码后不得出现原值
        const string schema = """[{"key":"amount","type":"money","sensitive":true}]""";
        var card = new CfCard { FDataJson = """{"amount":88}""" };
        var result = svc.Redact(new CardRedactionRequest { Card = card, CardSchemaJson = schema });

        using var doc = global::System.Text.Json.JsonDocument.Parse(result.RedactedDataJson);
        Assert.Equal("****", doc.RootElement.GetProperty("amount").GetString());
    }

    [Fact]
    public void MalformedDataJson_FailsClosedToEmpty()
    {
        var svc = new CardRedactionService();
        var card = new CfCard { FDataJson = "{not valid json" };
        var result = svc.Redact(new CardRedactionRequest { Card = card, CardSchemaJson = Schema });
        Assert.Equal("{}", result.RedactedDataJson);
    }

    [Fact]
    public void InitialDataJson_RedactedWithSameAllowlist()
    {
        var svc = new CardRedactionService();
        var card = new CfCard
        {
            FDataJson = """{"amount":88}""",
            FInitialDataJson = """{"amount":1,"payeeAccountNo":"6222021234567890123","srcOnly":"leak"}"""
        };
        var result = svc.Redact(new CardRedactionRequest { Card = card, CardSchemaJson = Schema });

        using var doc = global::System.Text.Json.JsonDocument.Parse(result.RedactedInitialDataJson);
        var root = doc.RootElement;
        Assert.Equal("**** **** **** 0123", root.GetProperty("payeeAccountNo").GetString()); // 敏感打码
        Assert.False(root.TryGetProperty("srcOnly", out _));                                   // schema 外移除
    }

    [Fact]
    public void OwnerEditMode_GivesClearViewForDraftOwner()
    {
        var svc = new CardRedactionService();
        var result = svc.Redact(new CardRedactionRequest { Card = Card(), CardSchemaJson = Schema, OwnerEditMode = true });
        using var doc = global::System.Text.Json.JsonDocument.Parse(result.RedactedDataJson);
        // 草稿所有者看到明文（敏感字段不打码），但 schema 外键仍移除
        Assert.Equal("6222021234567890123", doc.RootElement.GetProperty("payeeAccountNo").GetString());
        Assert.False(doc.RootElement.TryGetProperty("legacyNote", out _));
    }
}
