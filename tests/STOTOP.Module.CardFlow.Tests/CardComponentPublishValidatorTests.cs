using STOTOP.Module.CardFlow.Models.Schema;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests;

/// <summary>
/// 发布门禁后端镜像 CardComponentPublishValidator 的拒绝面回归。
/// </summary>
public class CardComponentPublishValidatorTests
{
    private static string Envelope(string componentsJson)
        => "{\"version\":2,\"fields\":[],\"components\":[" + componentsJson + "]}";

    [Fact]
    public void 空schema_通过()
    {
        Assert.Empty(CardComponentPublishValidator.Validate(null));
        Assert.Empty(CardComponentPublishValidator.Validate(""));
        Assert.Empty(CardComponentPublishValidator.Validate("   "));
    }

    [Fact]
    public void legacy裸字段数组_无组件_通过()
    {
        // 顶层数组 = legacy 仅字段，无 components
        var json = "[{\"key\":\"amount\",\"type\":\"money\"}]";
        Assert.Empty(CardComponentPublishValidator.Validate(json));
    }

    [Fact]
    public void 可发布组件_通过()
    {
        var json = Envelope("{\"id\":\"c1\",\"type\":\"text\",\"title\":\"单行输入\",\"binding\":{\"source\":\"cardField\",\"fieldKey\":\"remark\"},\"props\":{\"publishable\":true,\"componentStatus\":\"ready\"}}");
        Assert.Empty(CardComponentPublishValidator.Validate(json));
    }

    [Fact]
    public void publishable为false_被拒且带原因()
    {
        var json = Envelope("{\"id\":\"c1\",\"type\":\"placeholderControl\",\"title\":\"AI 辅助\",\"binding\":{\"source\":\"cardField\"},\"props\":{\"publishable\":false,\"unsupportedReason\":\"缺少运行态集成能力\"}}");
        var errors = CardComponentPublishValidator.Validate(json);
        var msg = Assert.Single(errors);
        Assert.Contains("AI 辅助", msg);
        Assert.Contains("暂未支持发布", msg);
        Assert.Contains("缺少运行态集成能力", msg);
    }

    [Fact]
    public void componentStatus为deferred_被拒()
    {
        // 存量组件：无 publishable 键，仅 componentStatus=deferred
        var json = Envelope("{\"id\":\"c1\",\"type\":\"formula\",\"title\":\"公式\",\"binding\":{\"source\":\"cardField\"},\"props\":{\"componentStatus\":\"deferred\"}}");
        var errors = CardComponentPublishValidator.Validate(json);
        Assert.Contains(errors, e => e.Contains("暂缓组件"));
    }

    [Fact]
    public void componentStatus为template_被拒()
    {
        var json = Envelope("{\"id\":\"c1\",\"type\":\"componentSuite\",\"title\":\"组合套件\",\"binding\":{\"source\":\"static\"},\"props\":{\"componentStatus\":\"template\"}}");
        var errors = CardComponentPublishValidator.Validate(json);
        Assert.Contains(errors, e => e.Contains("模板占位"));
    }

    [Fact]
    public void 存量placeholderControl按controlKind兜底_被拒()
    {
        // 无 publishable / componentStatus，靠 type=placeholderControl + controlKind 兜底
        var json = Envelope("{\"id\":\"c1\",\"type\":\"placeholderControl\",\"title\":\"流水号\",\"binding\":{\"source\":\"cardField\"},\"props\":{\"controlKind\":\"serialNumber\"}}");
        var errors = CardComponentPublishValidator.Validate(json);
        Assert.Contains(errors, e => e.Contains("暂缓占位组件"));
    }

    [Fact]
    public void 非法绑定来源_被拒()
    {
        var json = Envelope("{\"id\":\"c1\",\"type\":\"text\",\"title\":\"越界绑定\",\"binding\":{\"source\":\"externalApi\"},\"props\":{\"publishable\":true}}");
        var errors = CardComponentPublishValidator.Validate(json);
        Assert.Contains(errors, e => e.Contains("绑定来源非法") && e.Contains("externalApi"));
    }

    [Fact]
    public void 多个问题组件_聚合多条错误()
    {
        var json = Envelope(
            "{\"id\":\"c1\",\"type\":\"placeholderControl\",\"title\":\"A\",\"binding\":{\"source\":\"cardField\"},\"props\":{\"publishable\":false}}," +
            "{\"id\":\"c2\",\"type\":\"text\",\"title\":\"B\",\"binding\":{\"source\":\"cardField\"},\"props\":{\"publishable\":true}}," +
            "{\"id\":\"c3\",\"type\":\"formula\",\"title\":\"C\",\"binding\":{\"source\":\"cardField\"},\"props\":{\"componentStatus\":\"deferred\"}}");
        var errors = CardComponentPublishValidator.Validate(json);
        Assert.Equal(2, errors.Count);
    }
}
