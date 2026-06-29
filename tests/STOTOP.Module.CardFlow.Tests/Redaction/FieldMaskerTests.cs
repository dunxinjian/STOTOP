using STOTOP.Module.CardFlow.Services.Redaction;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Redaction;

public class FieldMaskerTests
{
    [Theory]
    [InlineData("13800138000", "phone", "138****8000")]
    [InlineData("110101199003075678", "idCard", "1101**********5678")]
    [InlineData("6222021234567890123", "bankCard", "**** **** **** 0123")]
    [InlineData("zhangsan@corp.com", "email", "z***@corp.com")]
    [InlineData("张三丰", "name", "张**")]
    [InlineData("ABCDEFGH", null, "AB****GH")]
    [InlineData("ABC", null, "****")]
    public void Mask_AppliesPattern(string input, string? pattern, string expected)
    {
        Assert.Equal(expected, FieldMasker.Mask(input, pattern));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Mask_EmptyReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, FieldMasker.Mask(input, "phone"));
    }

    [Fact]
    public void Mask_ShortPhoneFallsBackToGeneric()
    {
        Assert.Equal("****", FieldMasker.Mask("123", "phone"));
    }

    [Theory]
    [InlineData("ABCDE", null, "****")]            // 5-char generic 全打码（修复点）
    [InlineData("ABCDEFG", null, "****")]          // 7-char generic 全打码
    [InlineData("ABCDEFGH", null, "AB****GH")]     // 8-char generic 露边
    [InlineData("1234567", "phone", "****")]       // 7位 phone 退化为 generic→全打码
    [InlineData("12345678", "idCard", "12****78")] // 8位 idCard 退化为 generic(len=8>7)→露边
    [InlineData("1234", "bankCard", "****")]       // 4位 bankCard 全打码
    [InlineData("@x", "email", "****")]            // @在首位 → generic
    [InlineData("abc", "email", "****")]           // 无@ → generic(len3)→全打码
    [InlineData("李", "name", "李")]                // 单字 name 原样
    [InlineData("13800138000", " PHONE ", "138****8000")] // pattern 带空格/大写仍归一命中
    public void Mask_BoundaryCases(string input, string? pattern, string expected)
    {
        Assert.Equal(expected, FieldMasker.Mask(input, pattern));
    }
}
