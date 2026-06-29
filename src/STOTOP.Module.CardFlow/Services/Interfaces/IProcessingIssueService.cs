using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IProcessingIssueService
{
    Task<CfBatchError> ReportAsync(ProcessingIssueReportRequest request, long orgId);
    Task DispatchBatchAsync(long batchId, long orgId);
    Task<PagedResult<ProcessingIssueDto>> GetIssuesAsync(ProcessingIssueQueryRequest request, long orgId);
    Task<ProcessingIssueDto?> GetIssueAsync(long id, long orgId);
    Task ResolveAsync(long id, ProcessingIssueResolveRequest request, long userId, long orgId);
    Task IgnoreAsync(long id, ProcessingIssueResolveRequest request, long userId, long orgId);
    Task RetryAsync(long id, ProcessingIssueRetryRequest request, long userId, long orgId);
}

