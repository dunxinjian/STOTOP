using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.FileManager;

public class FileManagerService
{
    private readonly STOTOPDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FileManagerService> _logger;

    private const string UploadBaseDir = "uploads/datacenter";



    public FileManagerService(STOTOPDbContext context, IConfiguration configuration, ILogger<FileManagerService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>分页查询服务器上传文件列表</summary>
    public async Task<PagedResult<UploadedFileDto>> GetUploadedFilesAsync(FileQueryFilter filter)
    {
        var query = _context.Set<CfBatch>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(filter.Status) && int.TryParse(filter.Status, out var statusFilter))
            query = query.Where(b => b.FStatus == statusFilter);

        if (filter.StartDate.HasValue)
            query = query.Where(b => b.FCreatedTime >= filter.StartDate.Value);

        if (filter.EndDate.HasValue)
            query = query.Where(b => b.FCreatedTime <= filter.EndDate.Value);

        // SourceType 筛选：DC文件类型已废除，忽略该筛选条件
        // if (!string.IsNullOrEmpty(filter.SourceType)) { ... }

        var total = await query.CountAsync();

        // 排序
        query = filter.SortBy?.ToLower() switch
        {
            "filename" => filter.SortDesc ? query.OrderByDescending(b => b.FFileName) : query.OrderBy(b => b.FFileName),
            "filesize" => filter.SortDesc ? query.OrderByDescending(b => b.FFileSize) : query.OrderBy(b => b.FFileSize),
            "status" => filter.SortDesc ? query.OrderByDescending(b => b.FStatus) : query.OrderBy(b => b.FStatus),
            _ => filter.SortDesc ? query.OrderByDescending(b => b.FCreatedTime) : query.OrderBy(b => b.FCreatedTime)
        };

        var batches = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        // DC文件类型已废除，文件类型名称返回 null
        var fileTypes = new Dictionary<long, string>();

        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), UploadBaseDir);
        var items = batches.Select(b =>
        {
            var filePath = GetAbsoluteFilePath(b.FFilePath);
            var physicalExists = !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
            long actualSize = b.FFileSize ?? 0;
            if (physicalExists)
            {
                try { actualSize = new FileInfo(filePath!).Length; } catch { }
            }

            return new UploadedFileDto
            {
                BatchId = b.FID,
                FileName = b.FFileName ?? "",
                FileSize = actualSize,
                UploadTime = b.FCreatedTime,
                SourceType = null, // DC文件类型已废除
                BatchNo = b.FBatchNo,
                Status = b.FStatus.ToString(),
                StatusText = GetBatchStatusText(b.FStatus),
                PhysicalFileExists = physicalExists
            };
        }).ToList();

        return new PagedResult<UploadedFileDto>
        {
            Items = items,
            Total = total,
            PageIndex = filter.Page,
            PageSize = filter.PageSize
        };
    }

    /// <summary>获取单个文件详情</summary>
    public async Task<UploadedFileDto?> GetFileDetailAsync(long batchId)
    {
        var batch = await _context.Set<CfBatch>().AsNoTracking().FirstOrDefaultAsync(b => b.FID == batchId);
        if (batch == null) return null;

        string? fileTypeName = null; // DC文件类型已废除

        var filePath = GetAbsoluteFilePath(batch.FFilePath);
        var physicalExists = !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        long fileSize = batch.FFileSize ?? 0;
        if (physicalExists)
        {
            try { fileSize = new FileInfo(filePath!).Length; } catch { }
        }

        return new UploadedFileDto
        {
            BatchId = batch.FID,
            FileName = batch.FFileName ?? "",
            FileSize = fileSize,
            UploadTime = batch.FCreatedTime,
            SourceType = fileTypeName,
            BatchNo = batch.FBatchNo,
            Status = batch.FStatus.ToString(),
            StatusText = GetBatchStatusText(batch.FStatus),
            PhysicalFileExists = physicalExists
        };
    }

    /// <summary>批量删除服务器上的原始文件</summary>
    public async Task<int> DeleteFilesAsync(List<long> batchIds)
    {
        var batches = await _context.Set<CfBatch>()
            .Where(b => batchIds.Contains(b.FID))
            .ToListAsync();

        int deletedCount = 0;
        foreach (var batch in batches)
        {
            var filePath = GetAbsoluteFilePath(batch.FFilePath);
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    deletedCount++;
                    _logger.LogInformation("已删除文件: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "删除文件失败: {FilePath}", filePath);
                }
            }
        }

        return deletedCount;
    }

    /// <summary>存储空间统计</summary>
    public async Task<StorageStatsDto> GetStorageStatsAsync()
    {
        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), UploadBaseDir);
        var stats = new StorageStatsDto();

        if (!Directory.Exists(baseDir))
            return stats;

        var allFiles = Directory.GetFiles(baseDir, "*.*", SearchOption.AllDirectories);
        var monthlyDict = new Dictionary<string, (long size, int count)>();

        foreach (var file in allFiles)
        {
            try
            {
                var fi = new FileInfo(file);
                stats.TotalSize += fi.Length;
                stats.TotalFileCount++;

                var month = fi.LastWriteTime.ToString("yyyy-MM");
                if (monthlyDict.ContainsKey(month))
                {
                    var (size, count) = monthlyDict[month];
                    monthlyDict[month] = (size + fi.Length, count + 1);
                }
                else
                {
                    monthlyDict[month] = (fi.Length, 1);
                }
            }
            catch { }
        }

        stats.MonthlyStats = monthlyDict
            .OrderByDescending(kv => kv.Key)
            .Select(kv => new MonthlyStorageDto
            {
                Month = kv.Key,
                Size = kv.Value.size,
                FileCount = kv.Value.count
            })
            .ToList();

        return await Task.FromResult(stats);
    }

    /// <summary>清理策略列表</summary>
    public async Task<List<CleanupPolicyDto>> GetCleanupPoliciesAsync()
    {
        return await _context.Set<CfFileCleanupPolicy>()
            .AsNoTracking()
            .OrderByDescending(p => p.FCreatedTime)
            .Select(p => new CleanupPolicyDto
            {
                Id = p.FID,
                PolicyName = p.FPolicyName,
                RetentionDays = p.FRetentionDays,
                CronExpression = p.FCronExpression,
                Status = p.FStatus,
                LastExecuteTime = p.FLastExecutedTime,
                CreateTime = p.FCreatedTime
            })
            .ToListAsync();
    }

    /// <summary>创建/更新清理策略</summary>
    public async Task<CleanupPolicyDto> SaveCleanupPolicyAsync(SaveCleanupPolicyRequest request)
    {
        CfFileCleanupPolicy policy;

        if (request.Id.HasValue && request.Id.Value > 0)
        {
            policy = await _context.Set<CfFileCleanupPolicy>()
                .AsTracking()
                .FirstOrDefaultAsync(p => p.FID == request.Id.Value)
                ?? throw new InvalidOperationException("策略不存在");

            policy.FPolicyName = request.PolicyName;
            policy.FRetentionDays = request.RetentionDays;
            policy.FCronExpression = request.CronExpression;
            policy.FStatus = request.Status;
            policy.FUpdatedTime = DateTime.Now;
        }
        else
        {
            policy = new CfFileCleanupPolicy
            {
                FPolicyName = request.PolicyName,
                FRetentionDays = request.RetentionDays,
                FCronExpression = request.CronExpression,
                FStatus = request.Status,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            _context.Set<CfFileCleanupPolicy>().Add(policy);
        }

        await _context.SaveChangesAsync();

        // 管理 Hangfire 定时任务
        var jobId = $"file-cleanup-{policy.FID}";
        if (policy.FStatus == 1)
        {
            RecurringJob.AddOrUpdate<FileManagerService>(
                jobId,
                s => s.ExecuteCleanupByPolicyAsync(policy.FID, CancellationToken.None),
                policy.FCronExpression);
            policy.FHangfireJobId = jobId;
        }
        else
        {
            RecurringJob.RemoveIfExists(jobId);
            policy.FHangfireJobId = null;
        }

        await _context.SaveChangesAsync();

        return new CleanupPolicyDto
        {
            Id = policy.FID,
            PolicyName = policy.FPolicyName,
            RetentionDays = policy.FRetentionDays,
            CronExpression = policy.FCronExpression,
            Status = policy.FStatus,
            LastExecuteTime = policy.FLastExecutedTime,
            CreateTime = policy.FCreatedTime
        };
    }

    /// <summary>手动触发清理（执行所有启用的策略）</summary>
    public async Task<CleanupResultDto> ExecuteCleanupAsync()
    {
        var policies = await _context.Set<CfFileCleanupPolicy>()
            .Where(p => p.FStatus == 1)
            .ToListAsync();

        int totalDeleted = 0;
        long totalFreed = 0;

        foreach (var policy in policies)
        {
            var result = await ExecuteCleanupByPolicyAsync(policy.FID, CancellationToken.None);
            totalDeleted += result.DeletedFileCount;
            totalFreed += result.FreedSpace;
        }

        return new CleanupResultDto
        {
            DeletedFileCount = totalDeleted,
            FreedSpace = totalFreed
        };
    }

    /// <summary>按指定策略执行清理（Hangfire 调用入口）</summary>
    public async Task<CleanupResultDto> ExecuteCleanupByPolicyAsync(long policyId, CancellationToken ct)
    {
        var policy = await _context.Set<CfFileCleanupPolicy>()
            .FirstOrDefaultAsync(p => p.FID == policyId, ct);

        if (policy == null)
            return new CleanupResultDto();

        var cutoffDate = DateTime.Now.AddDays(-policy.FRetentionDays);

        var batches = await _context.Set<CfBatch>()
            .Where(b => b.FCreatedTime < cutoffDate && b.FStatus == CfBatchStatus.Completed)
            .ToListAsync(ct);

        int deletedCount = 0;
        long freedSpace = 0;

        foreach (var batch in batches)
        {
            var filePath = GetAbsoluteFilePath(batch.FFilePath);
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    var fi = new FileInfo(filePath);
                    var size = fi.Length;
                    File.Delete(filePath);
                    deletedCount++;
                    freedSpace += size;
                    _logger.LogInformation("清理策略 [{PolicyName}] 删除文件: {FilePath}", policy.FPolicyName, filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "清理策略 [{PolicyName}] 删除文件失败: {FilePath}", policy.FPolicyName, filePath);
                }
            }
        }

        // 更新策略执行时间
        policy.FLastExecutedTime = DateTime.Now;
        policy.FUpdatedTime = DateTime.Now;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("清理策略 [{PolicyName}] 执行完成，删除 {Count} 个文件，释放 {Size} 字节",
            policy.FPolicyName, deletedCount, freedSpace);

        return new CleanupResultDto
        {
            DeletedFileCount = deletedCount,
            FreedSpace = freedSpace
        };
    }

    /// <summary>清理预览（显示将被清理的文件）</summary>
    public async Task<CleanupPreviewDto> PreviewCleanupAsync()
    {
        var policies = await _context.Set<CfFileCleanupPolicy>()
            .Where(p => p.FStatus == 1)
            .ToListAsync();

        if (policies.Count == 0)
            return new CleanupPreviewDto();

        // 取最小保留天数作为预览阈值
        var minRetentionDays = policies.Min(p => p.FRetentionDays);
        var cutoffDate = DateTime.Now.AddDays(-minRetentionDays);

        var batches = await _context.Set<CfBatch>()
            .AsNoTracking()
            .Where(b => b.FCreatedTime < cutoffDate && b.FStatus == CfBatchStatus.Completed)
            .ToListAsync();

        int willDeleteCount = 0;
        long willFreeSpace = 0;
        var fileNames = new List<string>();

        foreach (var batch in batches)
        {
            var filePath = GetAbsoluteFilePath(batch.FFilePath);
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    var fi = new FileInfo(filePath);
                    willDeleteCount++;
                    willFreeSpace += fi.Length;
                    fileNames.Add(batch.FFileName ?? Path.GetFileName(filePath));
                }
                catch { }
            }
        }

        return new CleanupPreviewDto
        {
            WillDeleteCount = willDeleteCount,
            WillFreeSpace = willFreeSpace,
            FileNames = fileNames
        };
    }

    private static string GetBatchStatusText(int status) => status switch
    {
        CfBatchStatus.Parsing => "解析中",
        CfBatchStatus.Staged => "已暂存",
        CfBatchStatus.QualityChecking => "质检中",
        CfBatchStatus.CardCreated => "已创建卡片",
        CfBatchStatus.Processing => "处理中",
        CfBatchStatus.Completed => "已完成",
        CfBatchStatus.Failed => "失败",
        CfBatchStatus.PartiallyCompleted => "部分完成",
        CfBatchStatus.Revoked => "已撤销",
        _ => "未知"
    };

    #region 辅助方法

    private static string? GetAbsoluteFilePath(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return null;

        // 如果已经是绝对路径，直接返回
        if (Path.IsPathRooted(relativePath))
            return relativePath;

        return Path.Combine(Directory.GetCurrentDirectory(), relativePath);
    }

    #endregion
}
