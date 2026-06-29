using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.Dispatch;

public class DispatchService : IDispatchService
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<DispatchService> _logger;

    public DispatchService(STOTOPDbContext context, ILogger<DispatchService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<ImportErrorDto>> GetImportErrorsAsync(ImportErrorQueryDto query)
    {
        var q = _context.Set<CfBatchError>()
            .AsNoTracking()
            .AsQueryable();

        // 批次ID筛选
        if (query.BatchId.HasValue)
        {
            q = q.Where(e => e.FBatchId == query.BatchId.Value);
        }

        // 错误类型筛选
        if (!string.IsNullOrEmpty(query.ErrorType))
        {
            q = q.Where(e => e.FErrorType == query.ErrorType);
        }

        // 严重级别筛选
        if (!string.IsNullOrEmpty(query.SeverityLevel))
        {
            q = q.Where(e => e.FSeverityLevel == query.SeverityLevel);
        }

        // 派发状态筛选
        if (!string.IsNullOrEmpty(query.DispatchStatus))
        {
            q = q.Where(e => e.FDispatchStatus == query.DispatchStatus);
        }

        // 日期筛选
        if (query.StartDate.HasValue)
        {
            q = q.Where(e => e.FCreatedTime >= query.StartDate.Value);
        }
        if (query.EndDate.HasValue)
        {
            var endDate = query.EndDate.Value.AddDays(1);
            q = q.Where(e => e.FCreatedTime < endDate);
        }

        // 关键字搜索（批次号/文件名）
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(e => _context.Set<CfBatch>()
                .Any(b => b.FID == e.FBatchId &&
                     (b.FBatchNo != null && b.FBatchNo.Contains(keyword) ||
                      (b.FFileName != null && b.FFileName.Contains(keyword)))));
        }

        // 查询总数
        var total = await q.CountAsync();

        // 分页查询
        var errors = await q
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        // 批量获取批次信息
        var batchIds = errors.Select(e => e.FBatchId).Distinct().ToList();
        var batches = await _context.Set<CfBatch>()
            .Where(b => batchIds.Contains(b.FID))
            .ToDictionaryAsync(b => b.FID, b => new { b.FBatchNo, b.FFileName });

        return new PagedResult<ImportErrorDto>
        {
            Items = errors.Select(e => new ImportErrorDto
            {
                Id = e.FID,
                BatchId = e.FBatchId,
                BatchNo = batches.TryGetValue(e.FBatchId, out var batch) ? batch.FBatchNo : null,
                FileName = batches.TryGetValue(e.FBatchId, out var batch2) ? batch2.FFileName : null,
                RowNumber = e.FRowNumber,
                ErrorType = e.FErrorType,
                SeverityLevel = e.FSeverityLevel,
                ErrorField = e.FErrorField,
                ErrorMessage = e.FErrorMessage,
                SuggestedFix = e.FSuggestedFix,
                OriginalValue = e.FOriginalValue,
                QualityDimension = e.FQualityDimension,
                DispatchStatus = e.FDispatchStatus,
                DispatchType = e.FDispatchType,
                DispatchRecordId = e.FDispatchRecordId,
                CreateTime = e.FCreatedTime
            }).ToList(),
            Total = total,
            PageIndex = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<ImportOverviewDto> GetImportOverviewAsync()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        // 今日批次统计
        var todayBatches = await _context.Set<CfBatch>()
            .Where(b => b.FCreatedTime >= today && b.FCreatedTime < tomorrow)
            .ToListAsync();

        var todayBatchCount = todayBatches.Count;
        var todayTotalRows = todayBatches.Sum(b => b.FTotalRows);
        var todaySuccessRows = todayBatches.Sum(b => b.FSuccessRows);
        var successRate = todayTotalRows > 0 ? (double)todaySuccessRows / todayTotalRows * 100 : 0;

        // 待处理异常数量
        var pendingExceptionCount = await _context.Set<CfBatchError>()
            .Where(e => string.IsNullOrEmpty(e.FDispatchStatus) || e.FDispatchStatus == "Pending")
            .CountAsync();

        // 处理中任务数量
        var processingTaskCount = await _context.Set<CfBusinessDispatchRecord>()
            .Where(r => r.FStatus == "Pending" || r.FStatus == "Processing")
            .CountAsync();

        // 最近7天趋势
        var trendStartDate = today.AddDays(-6);
        var dailyTrend = new List<DailyImportTrendDto>();

        for (var date = trendStartDate; date <= today; date = date.AddDays(1))
        {
            var nextDate = date.AddDays(1);
            var importCount = await _context.Set<CfBatch>()
                .Where(b => b.FCreatedTime >= date && b.FCreatedTime < nextDate)
                .SumAsync(b => b.FTotalRows);

            var errorCount = await _context.Set<CfBatchError>()
                .Where(e => e.FCreatedTime >= date && e.FCreatedTime < nextDate)
                .CountAsync();

            dailyTrend.Add(new DailyImportTrendDto
            {
                Date = date.ToString("yyyy-MM-dd"),
                ImportCount = importCount,
                ErrorCount = errorCount
            });
        }

        return new ImportOverviewDto
        {
            TodayBatchCount = todayBatchCount,
            TodayTotalRows = todayTotalRows,
            SuccessRate = Math.Round(successRate, 2),
            PendingExceptionCount = pendingExceptionCount,
            ProcessingTaskCount = processingTaskCount,
            DailyTrend = dailyTrend
        };
    }

    public async Task<BusinessDispatchRecordDto> CreateDispatchAsync(CreateDispatchDto dto, string operatorName)
    {
        if (dto.ErrorIds.Length == 0)
            throw new ArgumentException("请选择至少一条错误记录");

        var firstErrorId = dto.ErrorIds[0];
        var firstError = await _context.Set<CfBatchError>()
            .AsTracking()
            .FirstOrDefaultAsync(e => e.FID == firstErrorId);

        if (firstError == null)
            throw new InvalidOperationException($"错误记录 {firstErrorId} 不存在");

        // 获取批次信息
        var batch = await _context.Set<CfBatch>()
            .FirstOrDefaultAsync(b => b.FID == firstError.FBatchId);

        var now = DateTime.Now;
        var record = new CfBusinessDispatchRecord
        {
            FBatchId = firstError.FBatchId,
            FBatchNo = batch?.FBatchNo,
            FErrorId = firstErrorId,
            FDispatchType = dto.DispatchType,
            FStatus = "Pending",
            FExceptionType = firstError.FErrorType,
            FSeverityLevel = firstError.FSeverityLevel,
            FDescription = dto.Description,
            FAssignee = dto.Assignee,
            FAssigneeName = dto.AssigneeName,
            FDeadline = dto.Deadline,
            FOperator = operatorName,
            FCreatedTime = now
        };

        _context.Set<CfBusinessDispatchRecord>().Add(record);
        await _context.SaveChangesAsync();

        // 更新错误记录的派发状态
        foreach (var errorId in dto.ErrorIds)
        {
            var error = await _context.Set<CfBatchError>()
                .AsTracking()
                .FirstOrDefaultAsync(e => e.FID == errorId);
            if (error != null)
            {
                error.FDispatchStatus = "Dispatched";
                error.FDispatchType = dto.DispatchType;
                error.FDispatchRecordId = record.FID;
            }
        }
        await _context.SaveChangesAsync();

        _logger.LogInformation("创建派发记录成功: RecordId={RecordId}, BatchNo={BatchNo}, ErrorCount={ErrorCount}",
            record.FID, record.FBatchNo, dto.ErrorIds.Length);

        return MapToDto(record);
    }

    public async Task<List<BusinessDispatchRecordDto>> CreateBatchDispatchAsync(CreateDispatchDto dto, string operatorName)
    {
        if (dto.ErrorIds.Length == 0)
            throw new ArgumentException("请选择至少一条错误记录");

        // 按批次分组创建派发记录
        var errors = await _context.Set<CfBatchError>()
            .AsTracking()
            .Where(e => dto.ErrorIds.Contains(e.FID))
            .ToListAsync();

        var batches = await _context.Set<CfBatch>()
            .Where(b => errors.Select(e => e.FBatchId).Distinct().Contains(b.FID))
            .ToDictionaryAsync(b => b.FID);

        var records = new List<CfBusinessDispatchRecord>();
        var now = DateTime.Now;

        // 按批次分组，每个批次创建一个派发记录
        foreach (var group in errors.GroupBy(e => e.FBatchId))
        {
            var batchId = group.Key;
            var batchErrors = group.ToList();
            var firstError = batchErrors.First();

            var record = new CfBusinessDispatchRecord
            {
                FBatchId = batchId,
                FBatchNo = batches.TryGetValue(batchId, out var b) ? b.FBatchNo : null,
                FErrorId = firstError.FID,
                FDispatchType = dto.DispatchType,
                FStatus = "Pending",
                FExceptionType = firstError.FErrorType,
                FSeverityLevel = firstError.FSeverityLevel,
                FDescription = dto.Description,
                FAssignee = dto.Assignee,
                FAssigneeName = dto.AssigneeName,
                FDeadline = dto.Deadline,
                FOperator = operatorName,
                FCreatedTime = now
            };

            _context.Set<CfBusinessDispatchRecord>().Add(record);
            records.Add(record);

            // 更新该批次下所有选中错误的派发状态
            foreach (var error in batchErrors)
            {
                error.FDispatchStatus = "Dispatched";
                error.FDispatchType = dto.DispatchType;
            }
        }

        await _context.SaveChangesAsync();

        // 更新所有错误的派发记录ID
        foreach (var record in records)
        {
            var relatedErrors = errors.Where(e => e.FBatchId == record.FBatchId).ToList();
            foreach (var error in relatedErrors)
            {
                error.FDispatchRecordId = record.FID;
            }
        }
        await _context.SaveChangesAsync();

        _logger.LogInformation("批量创建派发记录成功: 共 {Count} 条", records.Count);

        return records.Select(MapToDto).ToList();
    }

    public async Task<PagedResult<BusinessDispatchRecordDto>> GetDispatchRecordsAsync(int page, int pageSize, string? status, long? batchId)
    {
        var q = _context.Set<CfBusinessDispatchRecord>()
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            q = q.Where(r => r.FStatus == status);
        }

        if (batchId.HasValue)
        {
            q = q.Where(r => r.FBatchId == batchId.Value);
        }

        var total = await q.CountAsync();

        var records = await q
            .OrderByDescending(r => r.FCreatedTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<BusinessDispatchRecordDto>
        {
            Items = records.Select(MapToDto).ToList(),
            Total = total,
            PageIndex = page,
            PageSize = pageSize
        };
    }

    public async Task<BusinessDispatchRecordDto?> UpdateDispatchStatusAsync(long id, string status, string? result)
    {
        var record = await _context.Set<CfBusinessDispatchRecord>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (record == null)
            return null;

        record.FStatus = status;
        record.FResult = result;
        record.FUpdatedTime = DateTime.Now;

        if (status == "Completed")
        {
            record.FCompletedTime = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("更新派发记录状态: RecordId={RecordId}, Status={Status}", id, status);

        return MapToDto(record);
    }

    public async Task<int> BatchIgnoreErrorsAsync(long[] errorIds)
    {
        if (errorIds.Length == 0)
            return 0;

        var errors = await _context.Set<CfBatchError>()
            .AsTracking()
            .Where(e => errorIds.Contains(e.FID))
            .ToListAsync();

        foreach (var error in errors)
        {
            error.FDispatchStatus = "Ignored";
        }

        var count = await _context.SaveChangesAsync();

        _logger.LogInformation("批量忽略错误: Count={Count}", errors.Count);

        return errors.Count;
    }

    private static BusinessDispatchRecordDto MapToDto(CfBusinessDispatchRecord record)
    {
        return new BusinessDispatchRecordDto
        {
            Id = record.FID,
            BatchId = record.FBatchId,
            BatchNo = record.FBatchNo,
            ErrorId = record.FErrorId,
            DispatchType = record.FDispatchType,
            TargetType = record.FTargetType,
            TargetId = record.FTargetId,
            Assignee = record.FAssignee,
            AssigneeName = record.FAssigneeName,
            Status = record.FStatus,
            ExceptionType = record.FExceptionType,
            SeverityLevel = record.FSeverityLevel,
            Description = record.FDescription,
            Result = record.FResult,
            Deadline = record.FDeadline,
            CompletedTime = record.FCompletedTime,
            Operator = record.FOperator,
            CreateTime = record.FCreatedTime,
            UpdateTime = record.FUpdatedTime
        };
    }
}
