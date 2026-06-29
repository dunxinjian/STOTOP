using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public class ProcessingIssueService : IProcessingIssueService
{
    private readonly STOTOPDbContext _context;
    private readonly IQualityDispatchService _dispatchService;
    private readonly ILogger<ProcessingIssueService> _logger;

    public ProcessingIssueService(
        STOTOPDbContext context,
        IQualityDispatchService dispatchService,
        ILogger<ProcessingIssueService> logger)
    {
        _context = context;
        _dispatchService = dispatchService;
        _logger = logger;
    }

    public async Task<CfBatchError> ReportAsync(ProcessingIssueReportRequest request, long orgId)
    {
        if (request.BatchId <= 0)
            throw new ArgumentException("批次ID不能为空", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ErrorType))
            throw new ArgumentException("异常类型不能为空", nameof(request));

        var issue = new CfBatchError
        {
            FBatchId = request.BatchId,
            FStagingId = request.StagingId,
            FRowNumber = request.RowNumber,
            FErrorType = request.ErrorType.Trim(),
            FSeverityLevel = request.SeverityLevel,
            FErrorField = request.ErrorField,
            FErrorMessage = request.ErrorMessage,
            FSuggestedFix = request.SuggestedFix,
            FOriginalValue = request.OriginalValue,
            FQualityDimension = request.QualityDimension,
            FCreatedTime = DateTime.Now,
            FDispatchStatus = "Pending",
            FIssueType = request.ErrorType.Trim(),
            FProcessResult = 0,
            FResolutionStatus = "Pending",
            FResolutionPayloadJson = request.ResolutionPayloadJson,
            FRetryStatus = "None",
            FOrgId = orgId
        };

        _context.Set<CfBatchError>().Add(issue);
        await _context.SaveChangesAsync();
        return issue;
    }

    public async Task DispatchBatchAsync(long batchId, long orgId)
    {
        var errors = await _context.Set<CfBatchError>()
            .Where(e => e.FBatchId == batchId
                && e.FOrgId == orgId
                && (e.FDispatchStatus == null || e.FDispatchStatus == "Pending")
                && e.FResolutionStatus != "Resolved"
                && e.FResolutionStatus != "Ignored")
            .ToListAsync();

        if (errors.Count == 0)
        {
            _logger.LogInformation("批次 {BatchId} 无待派发异常", batchId);
            return;
        }

        await _dispatchService.DispatchAsync(batchId, orgId, errors);
    }

    public async Task<PagedResult<ProcessingIssueDto>> GetIssuesAsync(ProcessingIssueQueryRequest request, long orgId)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _context.Set<CfBatchError>()
            .AsNoTracking()
            .Where(e => e.FOrgId == orgId);

        if (request.BatchId.HasValue)
            query = query.Where(e => e.FBatchId == request.BatchId.Value);
        if (!string.IsNullOrWhiteSpace(request.ErrorType))
            query = query.Where(e => e.FErrorType == request.ErrorType);
        if (!string.IsNullOrWhiteSpace(request.SeverityLevel))
            query = query.Where(e => e.FSeverityLevel == request.SeverityLevel);
        if (!string.IsNullOrWhiteSpace(request.ResolutionStatus))
            query = query.Where(e => e.FResolutionStatus == request.ResolutionStatus);
        if (!string.IsNullOrWhiteSpace(request.DispatchStatus))
            query = query.Where(e => e.FDispatchStatus == request.DispatchStatus);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(e =>
                e.FErrorType.Contains(keyword)
                || (e.FErrorMessage != null && e.FErrorMessage.Contains(keyword))
                || (e.FOriginalValue != null && e.FOriginalValue.Contains(keyword)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var issueTypes = await LoadIssueTypesAsync(items.Select(e => e.FErrorType));
        return new PagedResult<ProcessingIssueDto>
        {
            Items = items.Select(e => MapToDto(e, issueTypes)).ToList(),
            Total = total,
            PageIndex = page,
            PageSize = pageSize
        };
    }

    public async Task<ProcessingIssueDto?> GetIssueAsync(long id, long orgId)
    {
        var issue = await _context.Set<CfBatchError>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.FID == id && e.FOrgId == orgId);
        if (issue == null) return null;

        var issueTypes = await LoadIssueTypesAsync(new[] { issue.FErrorType });
        return MapToDto(issue, issueTypes);
    }

    public async Task ResolveAsync(long id, ProcessingIssueResolveRequest request, long userId, long orgId)
    {
        var issue = await FindIssueForUpdateAsync(id, orgId);
        var now = DateTime.Now;

        issue.FResolutionStatus = "Resolved";
        issue.FResolutionPayloadJson = request.PayloadJson ?? request.Message ?? issue.FResolutionPayloadJson;
        issue.FResolvedBy = userId;
        issue.FResolvedTime = now;
        issue.FDispatchStatus = "Completed";
        issue.FProcessResult = 1;

        await _context.SaveChangesAsync();
    }

    public async Task IgnoreAsync(long id, ProcessingIssueResolveRequest request, long userId, long orgId)
    {
        var issue = await FindIssueForUpdateAsync(id, orgId);
        var now = DateTime.Now;

        issue.FResolutionStatus = "Ignored";
        issue.FResolutionPayloadJson = request.PayloadJson ?? request.Message ?? issue.FResolutionPayloadJson;
        issue.FResolvedBy = userId;
        issue.FResolvedTime = now;
        issue.FDispatchStatus = "Ignored";
        issue.FProcessResult = 2;

        await _context.SaveChangesAsync();
    }

    public async Task RetryAsync(long id, ProcessingIssueRetryRequest request, long userId, long orgId)
    {
        var issue = await FindIssueForUpdateAsync(id, orgId);
        var now = DateTime.Now;

        issue.FRetryStatus = "Requested";
        issue.FRetryMessage = string.IsNullOrWhiteSpace(request.Message)
            ? $"用户 {userId} 请求重跑: {request.RetryAction ?? "RetryPlugin"}"
            : request.Message;
        issue.FResolutionPayloadJson = request.PayloadJson ?? issue.FResolutionPayloadJson;
        issue.FResolutionStatus = "Processing";
        issue.FResolvedBy = userId;
        issue.FResolvedTime = now;

        await _context.SaveChangesAsync();
    }

    private async Task<CfBatchError> FindIssueForUpdateAsync(long id, long orgId)
    {
        return await _context.Set<CfBatchError>()
            .AsTracking()
            .FirstOrDefaultAsync(e => e.FID == id && e.FOrgId == orgId)
            ?? throw new InvalidOperationException("异常记录不存在");
    }

    private async Task<Dictionary<string, CfQualityIssueType>> LoadIssueTypesAsync(IEnumerable<string> codes)
    {
        var codeList = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (codeList.Count == 0)
            return new Dictionary<string, CfQualityIssueType>(StringComparer.OrdinalIgnoreCase);

        var types = await _context.Set<CfQualityIssueType>()
            .AsNoTracking()
            .Where(t => codeList.Contains(t.FCode))
            .ToListAsync();

        return types.ToDictionary(t => t.FCode, StringComparer.OrdinalIgnoreCase);
    }

    private static ProcessingIssueDto MapToDto(
        CfBatchError issue,
        IReadOnlyDictionary<string, CfQualityIssueType> issueTypes)
    {
        issueTypes.TryGetValue(issue.FErrorType, out var type);

        return new ProcessingIssueDto
        {
            Id = issue.FID,
            BatchId = issue.FBatchId,
            StagingId = issue.FStagingId,
            RowNumber = issue.FRowNumber,
            ErrorType = issue.FErrorType,
            IssueName = type?.FName,
            SeverityLevel = issue.FSeverityLevel,
            ErrorField = issue.FErrorField,
            ErrorMessage = issue.FErrorMessage,
            SuggestedFix = issue.FSuggestedFix ?? type?.FSuggestedFix,
            OriginalValue = issue.FOriginalValue,
            QualityDimension = issue.FQualityDimension,
            DispatchStatus = issue.FDispatchStatus,
            DispatchType = issue.FDispatchType,
            DispatchRecordId = issue.FDispatchRecordId,
            WorkItemId = issue.FWorkItemId,
            IssueType = issue.FIssueType,
            ProcessResult = issue.FProcessResult,
            ResolutionStatus = issue.FResolutionStatus,
            ResolutionPayloadJson = issue.FResolutionPayloadJson,
            ResolvedBy = issue.FResolvedBy,
            ResolvedTime = issue.FResolvedTime,
            RetryStatus = issue.FRetryStatus,
            RetryMessage = issue.FRetryMessage,
            ResolveMode = type?.FResolveMode,
            DetailRoute = type?.FDetailRoute,
            CardFlowCode = type?.FCardFlowCode,
            CardTemplateCode = type?.FCardTemplateCode,
            ActionSchemaJson = type?.FActionSchemaJson,
            AfterResolvedAction = type?.FAfterResolvedAction,
            AggregationMode = type?.FAggregationMode,
            OrgId = issue.FOrgId,
            CreatedTime = issue.FCreatedTime
        };
    }
}

