using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.Workflow.Services.Interfaces;

public interface IIssueAggregator
{
    // Agent 完成后汇报问题
    Task<WfIssuePack?> AggregateAsync(AggregateIssuesRequest request);

    // 更新问题解决状态
    Task ResolveIssueAsync(long issueDetailId, long resolverId, string? correctedValue = null);

    // 批量解决
    Task BatchResolveAsync(long issuePackId, long resolverId);

    // 获取问题包详情
    Task<IssuePackDto?> GetIssuePackAsync(long issuePackId);
    Task<List<IssuePackDto>> GetIssuePacksByChainAsync(string chainId);
}
