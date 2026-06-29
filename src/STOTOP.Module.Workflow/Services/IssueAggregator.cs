using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Entities;
using STOTOP.Module.Workflow.Enums;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.Workflow.Services;

public class IssueAggregator : IIssueAggregator
{
    private readonly STOTOPDbContext _db;
    private readonly IWorkItemService _workItemService;
    private readonly ILogger<IssueAggregator> _logger;

    public IssueAggregator(STOTOPDbContext db, IWorkItemService workItemService, ILogger<IssueAggregator> logger)
    {
        _db = db;
        _workItemService = workItemService;
        _logger = logger;
    }

    public async Task<WfIssuePack?> AggregateAsync(AggregateIssuesRequest request)
    {
        if (request.Issues.Count == 0)
        {
            _logger.LogInformation("问题列表为空，跳过聚合");
            return null;
        }

        // === 去重逻辑：查询 24小时内同 IssueType + 同组织 + 状态=Open 的 WorkItem，存在则追加关联而非新建 ===
        WfWorkItem? existingWorkItem = null;
        if (request.AutoCreateWorkItem)
        {
            var cutoff = DateTime.Now.AddHours(-24);
            existingWorkItem = await _db.Set<WfWorkItem>()
                .Where(w => w.FModule == "DataCenter"
                    && w.FBizType == request.IssueType
                    && w.FOrgId == request.OrgId
                    && (w.FStatus == (int)WorkItemStatus.Pending || w.FStatus == (int)WorkItemStatus.InProgress)
                    && w.FCreateTime >= cutoff)
                .OrderByDescending(w => w.FCreateTime)
                .FirstOrDefaultAsync();
        }

        // 创建问题包
        var pack = new WfIssuePack
        {
            FOrgId = request.OrgId,
            FChainId = request.ChainId,
            FIssueType = request.IssueType,
            FBatchId = request.BatchId,
            FTotalCount = request.Issues.Count,
            FResolvedCount = 0,
            FSummary = $"[{request.IssueType}] 共 {request.Issues.Count} 个问题"
        };

        _db.Set<WfIssuePack>().Add(pack);
        await _db.SaveChangesAsync();

        // 批量创建问题明细
        var details = request.Issues.Select(issue => new WfIssueDetail
        {
            FIssuePackId = pack.FID,
            FDataScopeId = request.DataScopeId,
            FRowId = issue.RowId,
            FTableName = issue.TableName,
            FErrorType = issue.ErrorType,
            FErrorMessage = issue.ErrorMessage,
            FFieldName = issue.FieldName,
            FOriginalValue = issue.OriginalValue
        }).ToList();

        _db.Set<WfIssueDetail>().AddRange(details);
        await _db.SaveChangesAsync();

        // 如果需要自动创建/关联 WorkItem
        if (request.AutoCreateWorkItem)
        {
            try
            {
                long workItemId;

                if (existingWorkItem != null)
                {
                    // 去重命中：追加关联到已有 WorkItem，更新其描述
                    workItemId = existingWorkItem.FID;
                    existingWorkItem.FDescription = (existingWorkItem.FDescription ?? "") +
                        $"\n[追加] 批次#{request.BatchId}: {request.Issues.Count}条问题";
                    existingWorkItem.FUpdateTime = DateTime.Now;
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("去重命中，追加关联到已有 WorkItem {WorkItemId}", workItemId);
                }
                else
                {
                    // 新建 WorkItem
                    var workItem = await _workItemService.CreateAsync(new CreateWorkItemRequest
                    {
                        OrgId = request.OrgId,
                        Title = $"数据问题待处理: {request.IssueType} ({request.Issues.Count}条)",
                        Description = pack.FSummary,
                        Type = (int)WorkItemType.Task,
                        Source = (int)WorkItemSource.Pipeline,
                        Priority = (int)WorkItemPriority.Normal,
                        ChainId = request.ChainId,
                        DataScopeId = request.DataScopeId,
                        CreatorId = request.CreatorId ?? 0,
                        Module = "DataCenter",
                        BizType = request.IssueType,
                        BizId = request.BatchId,
                        DetailRoute = $"/datacenter/upload?batch={request.BatchId}",
                        AutoDispatch = true
                    });
                    workItemId = workItem.Id;
                }

                // 关联工作项到问题包
                pack.FWorkItemId = workItemId;
                await _db.SaveChangesAsync();

                // 回写批次的 F工作项ID
                if (request.BatchId.HasValue)
                {
                    await BackwriteBatchWorkItemIdAsync(request.BatchId.Value, workItemId);
                }

                // 回写 DcImportError 的 FWorkItemId
                await BackwriteImportErrorWorkItemIdAsync(request.BatchId, request.IssueType, workItemId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "自动创建/关联工作项失败，问题包 {IssuePackId} 将不关联工作项", pack.FID);
            }
        }

        _logger.LogInformation("问题聚合完成，包ID: {PackId}，共 {Count} 个问题", pack.FID, request.Issues.Count);
        return pack;
    }

    public async Task ResolveIssueAsync(long issueDetailId, long resolverId, string? correctedValue = null)
    {
        var detail = await _db.Set<WfIssueDetail>().FirstOrDefaultAsync(d => d.FID == issueDetailId)
            ?? throw new InvalidOperationException($"问题明细 {issueDetailId} 不存在");

        if (detail.FIsResolved)
            throw new InvalidOperationException("该问题已解决");

        detail.FIsResolved = true;
        detail.FResolverId = resolverId;
        detail.FCorrectedValue = correctedValue;
        detail.FResolvedTime = DateTime.Now;

        // 更新问题包的 ResolvedCount
        var pack = await _db.Set<WfIssuePack>().FirstOrDefaultAsync(p => p.FID == detail.FIssuePackId);
        if (pack != null)
        {
            pack.FResolvedCount += 1;
            pack.FUpdateTime = DateTime.Now;

            // 如果全部解决，且关联了工作项，则标记完成
            if (pack.FResolvedCount >= pack.FTotalCount && pack.FWorkItemId.HasValue)
            {
                try
                {
                    await _workItemService.CompleteAsync(pack.FWorkItemId.Value, resolverId, "所有问题已解决", enforceAssignee: false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "自动完成工作项 {WorkItemId} 失败", pack.FWorkItemId);
                }
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task BatchResolveAsync(long issuePackId, long resolverId)
    {
        var pack = await _db.Set<WfIssuePack>().FirstOrDefaultAsync(p => p.FID == issuePackId)
            ?? throw new InvalidOperationException($"问题包 {issuePackId} 不存在");

        var unresolvedDetails = await _db.Set<WfIssueDetail>()
            .Where(d => d.FIssuePackId == issuePackId && !d.FIsResolved)
            .ToListAsync();

        var now = DateTime.Now;
        foreach (var detail in unresolvedDetails)
        {
            detail.FIsResolved = true;
            detail.FResolverId = resolverId;
            detail.FResolvedTime = now;
        }

        pack.FResolvedCount = pack.FTotalCount;
        pack.FUpdateTime = now;

        await _db.SaveChangesAsync();

        // 如果关联了工作项，标记完成
        if (pack.FWorkItemId.HasValue)
        {
            try
            {
                await _workItemService.CompleteAsync(pack.FWorkItemId.Value, resolverId, "批量解决所有问题", enforceAssignee: false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "自动完成工作项 {WorkItemId} 失败", pack.FWorkItemId);
            }
        }

        _logger.LogInformation("批量解决问题包 {PackId}，共解决 {Count} 个问题", issuePackId, unresolvedDetails.Count);
    }

    public async Task<IssuePackDto?> GetIssuePackAsync(long issuePackId)
    {
        var pack = await _db.Set<WfIssuePack>()
            .Include(p => p.Details)
            .FirstOrDefaultAsync(p => p.FID == issuePackId);

        return pack == null ? null : MapToDto(pack);
    }

    public async Task<List<IssuePackDto>> GetIssuePacksByChainAsync(string chainId)
    {
        var packs = await _db.Set<WfIssuePack>()
            .Include(p => p.Details)
            .Where(p => p.FChainId == chainId)
            .OrderByDescending(p => p.FCreateTime)
            .ToListAsync();

        return packs.Select(MapToDto).ToList();
    }

    #region Private Helpers

    /// <summary>回写批次的 F工作项ID</summary>
    private async Task BackwriteBatchWorkItemIdAsync(long batchId, long workItemId)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE [CF批次] SET [F工作项ID] = {0} WHERE [FID] = {1} AND [F工作项ID] IS NULL",
                workItemId, batchId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "回写批次 {BatchId} 的 F工作项ID 失败", batchId);
        }
    }

    /// <summary>回写 DcImportError 记录的 FWorkItemId 和 F问题类型</summary>
    private async Task BackwriteImportErrorWorkItemIdAsync(long? batchId, string issueType, long workItemId)
    {
        if (!batchId.HasValue) return;
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE [CF批次错误] SET [F工作项ID] = {0}, [F问题类型] = {1} WHERE [F批次ID] = {2} AND [F工作项ID] IS NULL",
                workItemId, issueType, batchId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "回写导入错误记录的 WorkItemId 失败: BatchId={BatchId}", batchId);
        }
    }

    private static IssuePackDto MapToDto(WfIssuePack pack)
    {
        return new IssuePackDto
        {
            Id = pack.FID,
            Uid = pack.FUID,
            ChainId = pack.FChainId,
            WorkItemId = pack.FWorkItemId,
            IssueType = pack.FIssueType,
            BatchId = pack.FBatchId,
            TotalCount = pack.FTotalCount,
            ResolvedCount = pack.FResolvedCount,
            Summary = pack.FSummary,
            CreateTime = pack.FCreateTime,
            Details = pack.Details.Select(d => new IssueDetailDto
            {
                Id = d.FID,
                RowId = d.FRowId,
                TableName = d.FTableName,
                ErrorType = d.FErrorType,
                ErrorMessage = d.FErrorMessage,
                FieldName = d.FFieldName,
                OriginalValue = d.FOriginalValue,
                CorrectedValue = d.FCorrectedValue,
                IsResolved = d.FIsResolved,
                ResolverId = d.FResolverId,
                ResolvedTime = d.FResolvedTime
            }).ToList()
        };
    }

    #endregion
}
