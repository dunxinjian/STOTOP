using System.Text.Json;
using STOTOP.Module.CardFlow.Models.Schema;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Redaction;

public class SchemaSensitivityTests
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void CardFieldDefinitionV2_DeserializesSensitiveAndMaskPattern()
    {
        const string json = """[{"key":"payeeAccountNo","label":"收款账号","type":"text","sensitive":true,"maskPattern":"bankCard"}]""";
        var fields = JsonSerializer.Deserialize<List<CardFieldDefinitionV2>>(json, Opts)!;
        Assert.True(fields[0].Sensitive);
        Assert.Equal("bankCard", fields[0].MaskPattern);
    }

    [Fact]
    public void CardFieldDefinitionV2_DefaultsNonSensitive()
    {
        const string json = """[{"key":"amount","label":"金额","type":"money"}]""";
        var fields = JsonSerializer.Deserialize<List<CardFieldDefinitionV2>>(json, Opts)!;
        Assert.False(fields[0].Sensitive);
        Assert.Null(fields[0].MaskPattern);
    }
}
