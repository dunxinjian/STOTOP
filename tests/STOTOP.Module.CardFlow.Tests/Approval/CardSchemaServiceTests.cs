using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class CardSchemaServiceTests
{
    private readonly CardSchemaService _svc = new();

    [Fact]
    public void V2EnvelopeSchema_ValidData_Passes()
    {
        const string schema = """{"version":2,"fields":[{"key":"amount","label":"金额","type":"money","required":true}],"components":[]}""";
        var result = _svc.ValidateCardData(schema, """{"amount":100}""");
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void V2EnvelopeSchema_MissingRequired_FailsWithFieldError()
    {
        const string schema = """{"version":2,"fields":[{"key":"amount","label":"金额","type":"money","required":true}]}""";
        var result = _svc.ValidateCardData(schema, "{}");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("金额"));        // 证明真的解析到了 v2 字段（而非旧的"Schema定义解析失败"）
        Assert.DoesNotContain(result.Errors, e => e.Contains("Schema定义解析失败"));
    }

    [Fact]
    public void ArraySchema_StillWorks()
    {
        const string schema = """[{"key":"amount","label":"金额","type":"money","required":true}]""";
        Assert.True(_svc.ValidateCardData(schema, """{"amount":1}""").IsValid);
        Assert.False(_svc.ValidateCardData(schema, "{}").IsValid);
    }

    [Fact]
    public void UnparseableSchema_DoesNotHardBlock()
    {
        var result = _svc.ValidateCardData("{not json", """{"x":1}""");
        Assert.True(result.IsValid);  // 解析不了 → best-effort 放行，不再误报"Schema定义解析失败"
    }
}
