using STOTOP.WebAPI.Dtos.WorkHub;
using STOTOP.WebAPI.Services;
using Xunit;

namespace STOTOP.WebAPI.Tests.WorkHub;

public class ResolveBizTypeTests
{
    private static WorkItemDto Item(string source, string category,
        Dictionary<string, object>? metadata = null) =>
        new() { Source = source, Category = category, Metadata = metadata ?? new() };

    [Fact]
    public void CardFlow_WithFlowName_UsesFlowNameAsLabel()
    {
        var (key, label) = WorkHubService.ResolveBizType(
            Item("cardflow", "approval", new() { ["flowName"] = "费用报销" }));
        Assert.Equal("flow:费用报销", key);
        Assert.Equal("费用报销", label);
    }

    [Fact]
    public void CardFlow_WithoutFlowName_FallsBackToApproval()
    {
        var (key, label) = WorkHubService.ResolveBizType(Item("cardflow", "approval"));
        Assert.Equal("approval", key);
        Assert.Equal("审批", label);
    }

    [Theory]
    [InlineData("finance", "approval", "voucher", "凭证复核")]
    [InlineData("quality", "alert", "quality", "质量异常")]
    [InlineData("contract", "reminder", "contract", "合同到期")]
    [InlineData("points", "approval", "points", "积分审批")]
    [InlineData("task", "notification", "notification", "通知")]
    [InlineData("task", "task", "task", "任务")]
    public void KnownSources_MapToBizType(string source, string category, string expectKey, string expectLabel)
    {
        var (key, label) = WorkHubService.ResolveBizType(Item(source, category));
        Assert.Equal(expectKey, key);
        Assert.Equal(expectLabel, label);
    }

    [Fact]
    public void Wf_WithBizType_UsesBizTypeMetadata()
    {
        var (key, label) = WorkHubService.ResolveBizType(
            Item("workflow", "approval", new() { ["bizType"] = "用印申请" }));
        Assert.Equal("wf:用印申请", key);
        Assert.Equal("用印申请", label);
    }

    [Fact]
    public void UnknownSource_WithoutMetadata_FallsBackToApproval()
    {
        var (key, label) = WorkHubService.ResolveBizType(Item("oa", "approval"));
        Assert.Equal("approval", key);
        Assert.Equal("审批", label);
    }

    [Fact]
    public void Datacenter_WithBizType_UsesBizTypeMetadata()
    {
        var (key, label) = WorkHubService.ResolveBizType(
            Item("datacenter", "approval", new() { ["bizType"] = "总部交易" }));
        Assert.Equal("wf:总部交易", key);
        Assert.Equal("总部交易", label);
    }

    [Fact]
    public void NullMetadata_FallsBackToApproval()
    {
        var item = new WorkItemDto { Source = "oa", Category = "approval", Metadata = null! };
        var (key, label) = WorkHubService.ResolveBizType(item);
        Assert.Equal("approval", key);
        Assert.Equal("审批", label);
    }
}
