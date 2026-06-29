using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class RouteGraphValidatorTests
{
    private static (string From, string To) E(string from, string to) => (from, to);

    [Fact]
    public void Validate_LinearDag_NoErrors()
    {
        var errors = RouteGraphValidator.Validate(
            new[] { "A", "B", "C" }, "A",
            new[] { E("A", "B"), E("B", "C") });
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_TwoNodeCycle_ReportsCycle()
    {
        var errors = RouteGraphValidator.Validate(
            new[] { "A", "B" }, "A",
            new[] { E("A", "B"), E("B", "A") });
        Assert.Single(errors);
        Assert.Contains("环", errors[0]);
        Assert.Contains("A", errors[0]);
        Assert.Contains("B", errors[0]);
    }

    [Fact]
    public void Validate_SelfLoop_ReportsCycle()
    {
        var errors = RouteGraphValidator.Validate(
            new[] { "A" }, "A",
            new[] { E("A", "A") });
        Assert.Single(errors);
        Assert.Contains("环", errors[0]);
        Assert.Contains("A", errors[0]);
    }

    [Fact]
    public void Validate_UnreachableNode_ReportsUnreachable()
    {
        var errors = RouteGraphValidator.Validate(
            new[] { "A", "B", "C" }, "A",
            new[] { E("A", "B") });   // C 孤立
        Assert.Single(errors);
        Assert.Contains("不可达", errors[0]);
        Assert.Contains("C", errors[0]);
    }

    [Fact]
    public void Validate_SingleNodeNoEdge_NoErrors()
    {
        var errors = RouteGraphValidator.Validate(
            new[] { "A" }, "A",
            new List<(string From, string To)>());
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_CycleAndIsolated_ReportsCycleOnly()
    {
        // 环 A<->B + 孤立 C：先报环，短路，不报不可达
        var errors = RouteGraphValidator.Validate(
            new[] { "A", "B", "C" }, "A",
            new[] { E("A", "B"), E("B", "A") });
        Assert.Single(errors);
        Assert.Contains("环", errors[0]);
        Assert.DoesNotContain("不可达", errors[0]);
    }

    [Fact]
    public void Validate_NullEntry_SkipsReachability()
    {
        // entry 为空 → 不查可达性（也不抛）；DAG 无环 → 无错
        var errors = RouteGraphValidator.Validate(
            new[] { "A", "B" }, null,
            new[] { E("A", "B") });
        Assert.Empty(errors);
    }
}
