using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Services;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Dispatch;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Services.Import;
using ExcelParserService = STOTOP.Module.CardFlow.Services.Import.ExcelParserService;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.System.Filters;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/import")]
public class CfImportController : ControllerBase
{
    private readonly IBatchTriggerService _batchTriggerService;
    private readonly IDispatchService _dispatchService;
    private readonly ILogger<CfImportController> _logger;
    private readonly STOTOPDbContext _context;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IProgressNotifier _progressNotifier;
    private readonly IConfiguration _configuration;
    private readonly BatchRevokeHandler _batchRevokeHandler;
    private readonly ExcelParserService _excelParser;

    public CfImportController(
        IBatchTriggerService batchTriggerService,
        IDispatchService dispatchService,
        ILogger<CfImportController> logger,
        STOTOPDbContext context,
        IServiceScopeFactory serviceScopeFactory,
        IProgressNotifier progressNotifier,
        IConfiguration configuration,
        BatchRevokeHandler batchRevokeHandler,
        ExcelParserService excelParser)
    {
        _batchTriggerService = batchTriggerService;
        _dispatchService = dispatchService;
        _logger = logger;
        _context = context;
        _serviceScopeFactory = serviceScopeFactory;
        _progressNotifier = progressNotifier;
        _configuration = configuration;
        _batchRevokeHandler = batchRevokeHandler;
        _excelParser = excelParser;
    }

    #region 文件上传

    /// <summary>
    /// 手动上传文件并创建导入批次
    /// 通过 BatchTriggerService 匹配流程定义 → 为每个匹配流程创建 CfBatch → 投递后台任务
    /// </summary>
    [HttpPost("upload")]
    [RequirePermission(CardFlowPermissions.ImportUpload)]
    public async Task<ApiResult<List<BatchTriggerResultDto>>> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return ApiResult<List<BatchTriggerResultDto>>.Fail("请上传文件");

        try
        {
            var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
            var operatorId = GetCurrentUserId();

            // 1. 保存上传文件到磁盘
            var batchNo = GenerateBatchNo();
            var uploadBaseDir = GetUploadBaseDir();
            var relativePath = Path.Combine(uploadBaseDir, DateTime.Now.ToString("yyyy-MM"), batchNo);
            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
            Directory.CreateDirectory(absolutePath);
            var filePath = Path.Combine(absolutePath, file.FileName);
            using (var outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                await file.CopyToAsync(outputStream);
            }

            // 2. 读取 Excel 表头用于流程匹配
            List<string> headers;
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                headers = _excelParser.ReadHeaders(fileStream, file.FileName);
            }

            // 3. 多流程匹配
            var matchResults = await _batchTriggerService.MatchFlowDefinitionsAsync(headers, file.FileName, currentOrgId);

            if (matchResults.Count == 0)
            {
                // 匹配失败，返回空列表（前端通过空列表判断未匹配，展示候选列表）
                return ApiResult<List<BatchTriggerResultDto>>.Success(
                    new List<BatchTriggerResultDto>(),
                    "未找到匹配的流程定义，请选择流程");
            }

            // 4. 预加载流程名称（减少循环查询）
            var flowDefinitionIds = matchResults.Select(r => r.FlowDefinitionId).ToList();
            var flowNameMap = await _context.Set<CfFlowDefinition>()
                .Where(fd => flowDefinitionIds.Contains(fd.FID))
                .ToDictionaryAsync(fd => fd.FID, fd => fd.FFlowName);

            // 5. 为每个匹配流程各创建独立批次（共享同一文件引用）
            var results = new List<BatchTriggerResultDto>();
            foreach (var match in matchResults)
            {
                var batchId = await _batchTriggerService.TriggerByFileUploadAsync(
                    match.FlowDefinitionId, currentOrgId, operatorId, filePath,
                    new Dictionary<string, string>());

                // 补充文件信息到 CfBatch
                var batch = await _context.Set<CfBatch>().FindAsync(batchId);
                if (batch != null)
                {
                    batch.FFileName = file.FileName;
                    batch.FFileSize = file.Length;
                    batch.FBatchNo = batchNo;
                    batch.FUploadMethod = "manual";
                    await _context.SaveChangesAsync();
                }

                flowNameMap.TryGetValue(match.FlowDefinitionId, out var flowName);
                results.Add(new BatchTriggerResultDto
                {
                    BatchId = batchId,
                    FlowDefinitionId = match.FlowDefinitionId,
                    FlowName = flowName ?? string.Empty,
                });
            }

            return ApiResult<List<BatchTriggerResultDto>>.Success(results, "文件上传并创建批次成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<List<BatchTriggerResultDto>>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return ApiResult<List<BatchTriggerResultDto>>.Fail($"上传失败：{ex.Message}", 500);
        }
    }

    /// <summary>
    /// 导入预览（上传后返回前10行+类型识别）
    /// </summary>
    [HttpPost("preview")]
    [RequirePermission(CardFlowPermissions.ImportUpload)]
    public async Task<ApiResult<ImportPreviewDto>> Preview(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return ApiResult<ImportPreviewDto>.Fail("请上传文件");

        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(file.FileName));
        try
        {
            // 1. 保存到临时文件
            using (var input = file.OpenReadStream())
            using (var output = new FileStream(tempPath, FileMode.Create))
            {
                await input.CopyToAsync(output);
            }

            var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

            // 2. 计算文件 Hash 并检测重复
            string fileHash;
            using (var md5 = global::System.Security.Cryptography.MD5.Create())
            using (var fs = global::System.IO.File.OpenRead(tempPath))
            {
                var hash = await md5.ComputeHashAsync(fs);
                fileHash = Convert.ToHexString(hash).ToLowerInvariant();
            }

            var duplicate = await _context.Set<CfBatch>()
                .AsNoTracking()
                .Where(b => b.FFileHash == fileHash && !b.FIsRevoked && b.FOrgId == currentOrgId)
                .Select(b => new { b.FBatchNo })
                .FirstOrDefaultAsync();

            // 3. 读取表头和预览行
            List<string> headers;
            List<Dictionary<string, object?>> previewRows;
            using (var fs = global::System.IO.File.OpenRead(tempPath))
            {
                headers = _excelParser.ReadHeaders(fs, file.FileName);
            }
            using (var fs = global::System.IO.File.OpenRead(tempPath))
            {
                previewRows = _excelParser.ReadPreviewRows(fs, file.FileName, 1, 10);
            }

            // 4. 统计总行数
            int totalRows = 0;
            using (var fs = global::System.IO.File.OpenRead(tempPath))
            {
                await _excelParser.ParseAsync(fs, file.FileName, 1, 2, 10000, (batch, startRow) =>
                {
                    totalRows += batch.Count;
                    return Task.CompletedTask;
                });
            }

            return ApiResult<ImportPreviewDto>.Success(new ImportPreviewDto
            {
                ColumnNames = headers,
                PreviewRows = previewRows,
                TotalRows = totalRows,
                IsDuplicate = duplicate != null,
                DuplicateBatchNo = duplicate?.FBatchNo,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预览文件失败");
            return ApiResult<ImportPreviewDto>.Fail($"预览失败：{ex.Message}", 500);
        }
        finally
        {
            if (global::System.IO.File.Exists(tempPath))
                global::System.IO.File.Delete(tempPath);
        }
    }

    #endregion

    #region 批次列表与查询

    /// <summary>
    /// 批次列表（分页，支持状态和日期筛选）
    /// </summary>
    [HttpGet("batches")]
    [RequirePermission(CardFlowPermissions.UploadCenter)]
    public async Task<ApiResult<PagedResult<ImportBatchDto>>> GetBatches(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? status = null,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] string? targetTable = null,
        [FromQuery] bool noDateFilter = false)
    {
        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
        DateTime? start = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
        DateTime? end = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);

        var query = _context.Set<CfBatch>()
            .AsNoTracking()
            .Where(b => b.FOrgId == currentOrgId);

        if (!noDateFilter)
        {
            if (start.HasValue) query = query.Where(b => b.FTriggeredTime >= start.Value);
            if (end.HasValue) query = query.Where(b => b.FTriggeredTime <= end.Value.AddDays(1));
        }

        if (status.HasValue) query = query.Where(b => b.FStatus == status.Value);
        if (!string.IsNullOrEmpty(targetTable)) query = query.Where(b => b.FActualTargetTable == targetTable);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(b => b.FCreatedTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var flowIds = items.Where(b => b.FFlowDefinitionId > 0).Select(b => b.FFlowDefinitionId).Distinct().ToList();
        var flowNames = await _context.Set<CfFlowDefinition>()
            .AsNoTracking()
            .Where(fd => flowIds.Contains(fd.FID))
            .ToDictionaryAsync(fd => fd.FID, fd => fd.FFlowName);

        var dtos = items.Select(b =>
        {
            var dto = MapCfBatchToDto(b);
            dto.FileTypeName = flowNames.GetValueOrDefault(b.FFlowDefinitionId);
            return dto;
        }).ToList();

        return ApiResult<PagedResult<ImportBatchDto>>.Success(new PagedResult<ImportBatchDto>
        {
            Items = dtos,
            Total = totalCount,
            PageIndex = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// 变更感知对账接口 — 返回所有 FChangeVersion > since 的批次摘要（含已撤销）
    /// 前端定时调用以发现所有状态变更（包括新批次），无需拉全量数据
    /// </summary>
    [HttpGet("batch-sync")]
    [RequirePermission(CardFlowPermissions.UploadCenter)]
    public async Task<ApiResult<BatchSyncResponse>> GetBatchSync([FromQuery] long since = 0)
    {
        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

        var batches = await _context.Set<CfBatch>()
            .AsNoTracking()
            .Where(b => b.FChangeVersion > since && b.FOrgId == currentOrgId)
            .OrderBy(b => b.FChangeVersion)
            .Select(b => new BatchSyncItemDto
            {
                BatchId = b.FID,
                Version = b.FChangeVersion,
                Status = b.FStatus,
                FileName = b.FFileName,
                FileSize = b.FFileSize,
                CreateTime = b.FCreatedTime,
                CurrentNodeName = b.FCurrentNodeName,
                ProgressPercent = b.FProgressPercent ?? 0,
                ErrorMessage = b.FErrorMessage,
                IsRevoked = b.FIsRevoked,
                TotalRows = b.FTotalRows,
                SuccessCount = b.FSuccessRows,
                FailedCount = b.FFailedRows,
                SkippedCount = b.FSkipRows,
                ErrorCount = b.FFailedRows,
                ProcessedRows = b.FSuccessRows + b.FFailedRows + b.FSkipRows,
                CurrentStepName = b.FCurrentNodeName,
                // IsStale: CfBatch 无 FIsStale 字段，默认 false
                // FlowName 需要关联查询，后续补充
            })
            .ToListAsync();

        var batchIds = batches.Select(x => x.BatchId).ToList();

        // 使用更高效的方式获取 FlowName
        var batchFlowIds = await _context.Set<CfBatch>()
            .AsNoTracking()
            .Where(b => batchIds.Contains(b.FID))
            .ToDictionaryAsync(b => b.FID, b => b.FFlowDefinitionId);

        var flowNames = await _context.Set<CfFlowDefinition>()
            .AsNoTracking()
            .Where(fd => batchFlowIds.Values.Distinct().Contains(fd.FID))
            .ToDictionaryAsync(fd => fd.FID, fd => fd.FFlowName);

        foreach (var item in batches)
        {
            if (batchFlowIds.TryGetValue(item.BatchId, out var flowId) && flowNames.TryGetValue(flowId, out var name))
                item.FlowName = name;
        }

        if (batchIds.Count > 0)
        {
            var executions = await _context.Set<CfPluginExecution>()
                .AsNoTracking()
                .Where(e => batchIds.Contains(e.FBatchId))
                .OrderBy(e => e.FAutoPluginIndex)
                .ToListAsync();

            var trailMap = executions
                .GroupBy(e => e.FBatchId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new PluginTrailItem
                    {
                        Name = e.FAutoPluginName,
                        Index = e.FAutoPluginIndex,
                        Status = e.FStatus
                    }).ToList());

            foreach (var item in batches)
            {
                if (trailMap.TryGetValue(item.BatchId, out var plugins))
                    item.Plugins = plugins;
            }
        }

        var maxVersion = batches.Count > 0
            ? batches.Max(b => b.Version)
            : since;

        return ApiResult<BatchSyncResponse>.Success(new BatchSyncResponse
        {
            Batches = batches,
            MaxVersion = maxVersion
        });
    }

    /// <summary>
    /// 按日统计导入批次数量
    /// </summary>
    [HttpGet("batches/daily-counts")]
    [RequirePermission(CardFlowPermissions.UploadCenter)]
    public async Task<ApiResult<List<DailyBatchCountDto>>> GetDailyBatchCounts(
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
        var end = string.IsNullOrEmpty(endDate) ? DateTime.Today : DateTime.Parse(endDate);
        var start = string.IsNullOrEmpty(startDate) ? end.AddDays(-90) : DateTime.Parse(startDate);

        var result = await _context.Set<CfBatch>()
            .AsNoTracking()
            .Where(b => b.FOrgId == currentOrgId && b.FCreatedTime >= start && b.FCreatedTime < end.AddDays(1))
            .GroupBy(b => b.FCreatedTime.Date)
            .Select(g => new DailyBatchCountDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Count = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        return ApiResult<List<DailyBatchCountDto>>.Success(result);
    }

    /// <summary>
    /// 批次详情
    /// </summary>
    [HttpGet("batches/{id:long}")]
    [RequirePermission(CardFlowPermissions.UploadCenter)]
    public async Task<ApiResult<ImportBatchDetailDto>> GetBatchDetail(long id)
    {
        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
        var batch = await _context.Set<CfBatch>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.FID == id && b.FOrgId == currentOrgId);

        if (batch == null)
            return ApiResult<ImportBatchDetailDto>.Fail("批次不存在", 404);

        var flowName = await _context.Set<CfFlowDefinition>()
            .AsNoTracking()
            .Where(fd => fd.FID == batch.FFlowDefinitionId)
            .Select(fd => fd.FFlowName)
            .FirstOrDefaultAsync();

        var dto = new ImportBatchDetailDto
        {
            Id = batch.FID,
            BatchNo = batch.FBatchNo ?? string.Empty,
            FileName = batch.FFileName,
            FileTypeName = flowName,
            FileSize = batch.FFileSize ?? 0,
            TotalRows = batch.FTotalRows,
            SuccessRows = batch.FSuccessRows,
            FailRows = batch.FFailedRows,
            SkipRows = batch.FSkipRows,
            Status = batch.FStatus,
            UploadMethod = batch.FUploadMethod,
            ImportStartTime = batch.FImportStartTime,
            ImportEndTime = batch.FImportEndTime,
            CreateTime = batch.FCreatedTime,
            ErrorSummary = batch.FErrorMessage,
            ActualTargetTable = batch.FActualTargetTable,
            IsStale = IsCfBatchStale(batch),
            Version = batch.FChangeVersion,
            FilePath = batch.FFilePath,
        };

        return ApiResult<ImportBatchDetailDto>.Success(dto);
    }

    /// <summary>
    /// 批次错误列表
    /// </summary>
    [HttpGet("batches/{id:long}/errors")]
    [RequirePermission(CardFlowPermissions.UploadCenter)]
    public async Task<ApiResult<List<ImportErrorDto>>> GetBatchErrors(long id)
    {
        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
        var batch = await _context.Set<CfBatch>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.FID == id && b.FOrgId == currentOrgId);

        if (batch == null)
            return ApiResult<List<ImportErrorDto>>.Fail("批次不存在", 404);

        var errors = await _context.Set<CfBatchError>()
            .AsNoTracking()
            .Where(e => e.FBatchId == id)
            .OrderByDescending(e => e.FCreatedTime)
            .Select(e => new ImportErrorDto
            {
                Id = e.FID,
                BatchId = e.FBatchId,
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
            })
            .ToListAsync();

        return ApiResult<List<ImportErrorDto>>.Success(errors);
    }

    /// <summary>
    /// 触发批次处理（阶段2+3）
    /// </summary>
    [HttpPost("batches/{id:long}/process")]
    [RequirePermission(CardFlowPermissions.ImportProcess)]
    public async Task<ApiResult> ProcessBatch(long id)
    {
        try
        {
            var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
            var batch = await _context.Set<CfBatch>()
                .AsTracking()
                .FirstOrDefaultAsync(b => b.FID == id && b.FOrgId == currentOrgId);

            if (batch == null)
                return ApiResult.Fail("批次不存在", 404);

            // 仅已暂存(1)状态可触发后续处理
            if (batch.FStatus != CfBatchStatus.Staged)
                return ApiResult.Fail($"当前状态 {batch.FStatus} 不允许触发处理");

            await _batchTriggerService.ConfirmStagingAndFanOutAsync(id);
            return ApiResult.Ok("批次处理已触发");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return ApiResult.Fail($"批次处理失败：{ex.Message}", 500);
        }
    }

    /// <summary>初始化分片上传</summary>
    [HttpPost("upload/init")]
    [RequirePermission(CardFlowPermissions.ImportUpload)]
    public async Task<IActionResult> InitChunkUpload([FromBody] ChunkUploadInitDto dto)
    {
        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

        // 检测重复文件
        var duplicate = await _context.Set<CfBatch>()
            .AsNoTracking()
            .Where(b => b.FFileHash == dto.FileHash && !b.FIsRevoked && b.FOrgId == currentOrgId)
            .Select(b => new { b.FBatchNo, b.FFileName })
            .FirstOrDefaultAsync();

        var uploadId = Guid.NewGuid().ToString("N");
        var uploadBaseDir = GetUploadBaseDir();
        var chunksDir = Path.Combine(Directory.GetCurrentDirectory(), uploadBaseDir, "chunks", uploadId);
        Directory.CreateDirectory(chunksDir);

        // 保存元数据
        var metaPath = Path.Combine(chunksDir, "meta.json");
        var meta = new { dto.FileName, dto.FileSize, dto.TotalChunks, dto.FileHash, OrgId = currentOrgId };
        await global::System.IO.File.WriteAllTextAsync(metaPath, global::System.Text.Json.JsonSerializer.Serialize(meta));

        return Ok(ApiResult<ChunkUploadInitResultDto>.Success(new ChunkUploadInitResultDto
        {
            UploadId = uploadId,
            ExistingChunks = new List<int>(),
            IsDuplicate = duplicate != null,
            DuplicateBatchNo = duplicate?.FBatchNo,
            DuplicateFileName = duplicate?.FFileName,
        }));
    }

    /// <summary>上传分片</summary>
    [HttpPost("upload/chunk")]
    [RequirePermission(CardFlowPermissions.ImportUpload)]
    public async Task<IActionResult> UploadChunk([FromForm] string uploadId, [FromForm] int chunkIndex, IFormFile file)
    {
        var uploadBaseDir = GetUploadBaseDir();
        var chunksDir = Path.Combine(Directory.GetCurrentDirectory(), uploadBaseDir, "chunks", uploadId);

        if (!Directory.Exists(chunksDir))
            return Ok(ApiResult<object>.Fail("上传会话不存在或已过期", 400));

        var chunkPath = Path.Combine(chunksDir, $"{chunkIndex}.part");
        using (var stream = new FileStream(chunkPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(ApiResult<object>.Success(new { uploaded = true, chunkIndex }));
    }

    /// <summary>完成分片上传并自动导入（支持多流程匹配）</summary>
    [HttpPost("upload/complete")]
    [RequirePermission(CardFlowPermissions.ImportUpload)]
    public async Task<IActionResult> CompleteChunkUpload([FromBody] CompleteChunkUploadDto dto)
    {
        var uploadBaseDir = GetUploadBaseDir();
        var chunksDir = Path.Combine(Directory.GetCurrentDirectory(), uploadBaseDir, "chunks", dto.UploadId);

        if (!Directory.Exists(chunksDir))
            return Ok(ApiResult<List<BatchTriggerResultDto>>.Fail("上传会话不存在或已过期", 400));

        // 读取元数据
        var metaPath = Path.Combine(chunksDir, "meta.json");
        if (!global::System.IO.File.Exists(metaPath))
            return Ok(ApiResult<List<BatchTriggerResultDto>>.Fail("上传元数据丢失", 400));

        var metaJson = await global::System.IO.File.ReadAllTextAsync(metaPath);
        using var metaDoc = global::System.Text.Json.JsonDocument.Parse(metaJson);
        var meta = metaDoc.RootElement;
        var fileName = meta.GetProperty("FileName").GetString() ?? "unknown";
        var fileSize = meta.GetProperty("FileSize").GetInt64();
        var fileHash = meta.GetProperty("FileHash").GetString();

        // 合并分片
        var batchNo = GenerateBatchNo();
        var relativePath = Path.Combine(uploadBaseDir, DateTime.Now.ToString("yyyy-MM"), batchNo);
        var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
        Directory.CreateDirectory(absolutePath);
        var filePath = Path.Combine(absolutePath, fileName);

        var chunkFiles = Directory.GetFiles(chunksDir, "*.part")
            .Select(f => new { Path = f, Index = int.Parse(Path.GetFileNameWithoutExtension(f)) })
            .OrderBy(f => f.Index)
            .ToList();

        using (var output = new FileStream(filePath, FileMode.Create))
        {
            foreach (var chunk in chunkFiles)
            {
                using var input = global::System.IO.File.OpenRead(chunk.Path);
                await input.CopyToAsync(output);
            }
        }

        // 读取 Excel 表头用于流程匹配
        List<string> headers;
        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            headers = _excelParser.ReadHeaders(fileStream, fileName);
        }

        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
        var operatorId = GetCurrentUserId();

        // 多流程匹配
        var matchResults = await _batchTriggerService.MatchFlowDefinitionsAsync(headers, fileName, currentOrgId);

        if (matchResults.Count == 0)
        {
            // 未匹配到流程：创建一个待分配批次，返回 flowDefinitionId=0 标记
            var pendingBatch = new CfBatch
            {
                FFlowDefinitionId = 0,
                FOrgId = currentOrgId,
                FTriggeredById = operatorId,
                FFilePath = filePath,
                FFileName = fileName,
                FFileSize = fileSize,
                FBatchNo = batchNo,
                FUploadMethod = "chunk",
                FFileHash = fileHash,
                FStatus = CfBatchStatus.Staged,
                FCreatedTime = DateTime.Now,
            };
            _context.Set<CfBatch>().Add(pendingBatch);
            await _context.SaveChangesAsync();

            // 清理分片临时目录
            Directory.Delete(chunksDir, true);

            return Ok(ApiResult<List<BatchTriggerResultDto>>.Success(new List<BatchTriggerResultDto>
            {
                new() { BatchId = pendingBatch.FID, FlowDefinitionId = 0, FlowName = "" }
            }, "未找到匹配的流程定义，请手动选择"));
        }

        // 预加载流程名称
        var flowDefinitionIds = matchResults.Select(r => r.FlowDefinitionId).ToList();
        var flowNameMap = await _context.Set<CfFlowDefinition>()
            .Where(fd => flowDefinitionIds.Contains(fd.FID))
            .ToDictionaryAsync(fd => fd.FID, fd => fd.FFlowName);

        // 为每个匹配流程创建独立批次（共享同一文件引用）
        var results = new List<BatchTriggerResultDto>();
        foreach (var match in matchResults)
        {
            var batchId = await _batchTriggerService.TriggerByFileUploadAsync(
                match.FlowDefinitionId, currentOrgId, operatorId, filePath,
                new Dictionary<string, string>());

            var batch = await _context.Set<CfBatch>().FindAsync(batchId);
            if (batch != null)
            {
                batch.FFileName = fileName;
                batch.FFileSize = fileSize;
                batch.FBatchNo = batchNo;
                batch.FUploadMethod = "chunk";
                batch.FFileHash = fileHash;
                await _context.SaveChangesAsync();
            }

            flowNameMap.TryGetValue(match.FlowDefinitionId, out var flowName);
            results.Add(new BatchTriggerResultDto
            {
                BatchId = batchId,
                FlowDefinitionId = match.FlowDefinitionId,
                FlowName = flowName ?? string.Empty,
            });
        }

        // 清理分片临时目录
        Directory.Delete(chunksDir, true);

        return Ok(ApiResult<List<BatchTriggerResultDto>>.Success(results, matchResults.Count > 1 ? $"已触发 {matchResults.Count} 个流程" : "上传完成"));
    }

    /// <summary>获取指定批次的 Excel 表头列名</summary>
    [HttpGet("batch/{batchId:long}/headers")]
    [RequirePermission(CardFlowPermissions.UploadCenter)]
    public async Task<ApiResult<List<string>>> GetBatchHeaders(long batchId)
    {
        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
        var batch = await _context.Set<CfBatch>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.FID == batchId && b.FOrgId == currentOrgId);

        if (batch == null)
            return ApiResult<List<string>>.Fail("批次不存在", 404);

        if (string.IsNullOrEmpty(batch.FFilePath) || !global::System.IO.File.Exists(batch.FFilePath))
            return ApiResult<List<string>>.Fail("批次文件不存在", 404);

        try
        {
            using var stream = new FileStream(batch.FFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var headers = _excelParser.ReadHeaders(stream, batch.FFileName ?? Path.GetFileName(batch.FFilePath));
            return ApiResult<List<string>>.Success(headers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取批次 {BatchId} 表头失败, OrgId={OrgId}", batchId, currentOrgId);
            return ApiResult<List<string>>.Fail($"读取表头失败：{ex.Message}", 500);
        }
    }

    /// <summary>获取指定批次的 Excel 列名信息（支持指定表头行号）</summary>
    [HttpGet("batches/{batchId:long}/columns")]
    [RequirePermission(CardFlowPermissions.UploadCenter)]
    public async Task<ActionResult<BatchColumnsDto>> GetBatchColumns(long batchId, [FromQuery] int headerRow = 1)
    {
        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
        var batch = await _context.Set<CfBatch>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.FID == batchId && b.FOrgId == currentOrgId);

        if (batch == null)
            return Ok(ApiResult<BatchColumnsDto>.Fail("批次不存在", 404));

        if (string.IsNullOrEmpty(batch.FFilePath) || !global::System.IO.File.Exists(batch.FFilePath))
            return Ok(ApiResult<BatchColumnsDto>.Fail("批次文件不存在", 404));

        try
        {
            using var stream = new FileStream(batch.FFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var headers = _excelParser.ReadHeaders(stream, batch.FFileName ?? Path.GetFileName(batch.FFilePath), headerRow);

            return Ok(ApiResult<BatchColumnsDto>.Success(new BatchColumnsDto
            {
                ColumnNames = headers,
                HeaderRowNumber = headerRow,
                ColumnIdentifier = string.Join(",", headers),
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取批次 {BatchId} 列名失败, OrgId={OrgId}", batchId, currentOrgId);
            return Ok(ApiResult<BatchColumnsDto>.Fail($"读取列名失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 获取队列中的批次（处理中/失败等非终态）
    /// </summary>
    /// <param name="batchIds">可选：传入指定批次ID列表（逿号分隔），用于前端 reconcile 增量刷新</param>
    [HttpGet("batches/queue")]
    [RequirePermission(CardFlowPermissions.UploadCenter)]
    public async Task<ApiResult<List<ImportBatchDto>>> GetQueueBatches([FromQuery] string? batchIds = null)
    {
        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

        // 非终态: 解析中(0), 已暂存(1), 质检中(2), 已创建卡片(3), 处理中(4), 失败(6)
        var activeStatuses = new[] {
            CfBatchStatus.Parsing, CfBatchStatus.Staged, CfBatchStatus.QualityChecking,
            CfBatchStatus.CardCreated, CfBatchStatus.Processing, CfBatchStatus.Failed
        };

        IQueryable<CfBatch> query = _context.Set<CfBatch>()
            .AsNoTracking()
            .Where(b => b.FOrgId == currentOrgId && !b.FIsRevoked);

        // 如果传入 batchIds 参数，按指定 ID 列表过滤（用于前端 reconcile）
        if (!string.IsNullOrWhiteSpace(batchIds))
        {
            var ids = batchIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s.Trim(), out var id) ? id : 0L)
                .Where(id => id > 0)
                .ToList();
            query = query.Where(b => ids.Contains(b.FID));
        }
        else
        {
            query = query.Where(b => activeStatuses.Contains(b.FStatus));
        }

        var batches = await query
            .OrderByDescending(b => b.FCreatedTime)
            .ToListAsync();

        var flowIds = batches.Where(b => b.FFlowDefinitionId > 0).Select(b => b.FFlowDefinitionId).Distinct().ToList();
        var flowNames = await _context.Set<CfFlowDefinition>()
            .AsNoTracking()
            .Where(fd => flowIds.Contains(fd.FID))
            .ToDictionaryAsync(fd => fd.FID, fd => fd.FFlowName);

        // 确定需要加载 trail 的批次：非终态 + 最近7天内的终态
        var terminalStatuses = new[] { 5, 6, 7, 8 };
        var sevenDaysAgo = DateTime.Now.AddDays(-7);
        var needTrailBatchIds = batches
            .Where(b => !terminalStatuses.Contains(b.FStatus) || b.FUpdatedTime > sevenDaysAgo)
            .Select(b => b.FID)
            .ToList();

        // 分步查询插件执行记录（避免 N+1）
        Dictionary<long, List<PluginTrailItem>> trailMap = new();
        if (needTrailBatchIds.Count > 0)
        {
            var executions = await _context.Set<CfPluginExecution>()
                .AsNoTracking()
                .Where(e => needTrailBatchIds.Contains(e.FBatchId))
                .OrderBy(e => e.FAutoPluginIndex)
                .ToListAsync();

            trailMap = executions
                .GroupBy(e => e.FBatchId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new PluginTrailItem
                    {
                        Name = e.FAutoPluginName,
                        Index = e.FAutoPluginIndex,
                        Status = e.FStatus
                    }).ToList());
        }

        var dtos = batches.Select(b =>
        {
            var dto = MapCfBatchToDto(b);
            dto.FileTypeName = flowNames.GetValueOrDefault(b.FFlowDefinitionId);
            if (trailMap.TryGetValue(b.FID, out var plugins))
                dto.Plugins = plugins;
            return dto;
        }).ToList();
        return ApiResult<List<ImportBatchDto>>.Success(dtos);
    }

    #endregion

    #region 重试

    /// <summary>
    /// 重试失败或卡住的批次（兼容旧端点，默认 continue 模式）
    /// </summary>
    [HttpPost("batches/{id:long}/retry")]
    [RequirePermission(CardFlowPermissions.ImportProcess)]
    public async Task<IActionResult> RetryBatch(long id)
    {
        return await RetryFlow(id, new RetryRequest { Mode = "continue" });
    }

    /// <summary>
    /// 重试失败的导入批次（支持 continue / full-restart 两种模式）
    /// </summary>
    [HttpPost("batches/{id:long}/retry-pipeline")]
    [RequirePermission(CardFlowPermissions.ImportProcess)]
    public async Task<IActionResult> RetryFlow(long id, [FromBody] RetryRequest? request)
    {
        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
        var batch = await _context.Set<CfBatch>()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.FID == id && b.FOrgId == currentOrgId);

        if (batch == null)
            return NotFound(new { message = "批次不存在" });

        // 防止并发重复处理：处理中(4) 且未卡住
        if (batch.FStatus == CfBatchStatus.Processing
            && batch.FImportStartTime.HasValue
            && (DateTime.Now - batch.FImportStartTime.Value).TotalMinutes <= 10)
        {
            return Conflict(new { message = "批次当前正在处理中，请等待完成或超时10分钟后再重试" });
        }

        var mode = request?.Mode ?? "continue";

        // 判断是否可重试
        var retryableStatuses = new[] {
            CfBatchStatus.Failed,
            CfBatchStatus.PartiallyCompleted,
            CfBatchStatus.Parsing,    // 解析中卡住
            CfBatchStatus.Staged,     // 已暂存但未继续
        };

        var isStuck = (batch.FStatus == CfBatchStatus.Processing || batch.FStatus == CfBatchStatus.QualityChecking)
                      && batch.FImportStartTime.HasValue
                      && (DateTime.Now - batch.FImportStartTime.Value).TotalMinutes > 10;

        if (!retryableStatuses.Contains(batch.FStatus) && !isStuck)
            return BadRequest(new { message = $"当前状态 [{batch.FStatus}] 不允许重试" });

        if (mode == "full-restart")
        {
            // 清理子批次 STG 数据（如适用）
            await CleanChildBatchesAsync(id);

            // 重置进度
            batch.FCurrentBatchStageOrder = null;
            batch.FCurrentNodeName = null;
            batch.FSuccessRows = 0;
            batch.FFailedRows = 0;
        }

        // 通用状态重置
        batch.FErrorMessage = null;
        batch.FImportStartTime = DateTime.Now;
        batch.FStatus = CfBatchStatus.Processing;
        batch.FUpdatedTime = DateTime.Now;

        await _context.SaveChangesAsync();

        // 版本号递增（使用 SEQUENCE）
        try
        {
            var versions = await _context.Database
                .SqlQueryRaw<long>(
                    "UPDATE [CF批次] SET [F变更版本号] = NEXT VALUE FOR dbo.SEQ_BatchChange OUTPUT INSERTED.[F变更版本号] WHERE [FID] = {0}",
                    batch.FID)
                .ToListAsync();
            batch.FChangeVersion = versions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取 SEQ_BatchChange 版本号失败: BatchId={BatchId}, OrgId={OrgId}", id, currentOrgId);
            batch.FChangeVersion = 0;
        }

        // 推送状态变更
        try
        {
            await _progressNotifier.NotifyBatchStatusChangedAsync(id, CfBatchStatus.Processing, "Processing");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "推送状态变更失败: BatchId={BatchId}, OrgId={OrgId}", id, currentOrgId);
        }

        // 通过 BatchTriggerService 重新投递任务
        // 检测是否有 batchAuto 节点来决定任务类型
        var hasBatchAuto = await _context.Set<CfStageDefinition>()
            .Join(_context.Set<CfFlowVersion>(),
                s => s.FFlowVersionId,
                v => v.FID,
                (s, v) => new { s, v })
            .AnyAsync(x => x.v.FFlowDefinitionId == batch.FFlowDefinitionId
                           && x.v.FIsCurrentVersion
                           && (x.s.FType == "batchAuto"
                               || (x.s.FType == "auto" && x.s.F处理粒度 == "batch")));

        // 直接通过 Channel 投递（使用 BatchTriggerService 内部 channel）
        // 这里采用 ServiceScopeFactory 获取 BatchTriggerService 来触发
        using var scope = _serviceScopeFactory.CreateScope();
        var triggerService = scope.ServiceProvider.GetRequiredService<IBatchTriggerService>();
        var jobKind = hasBatchAuto ? BatchJobKind.ProcessBatchStages : BatchJobKind.ParseAndStage;
        await triggerService.ProcessBatchJobAsync(new BatchJob(id, jobKind), HttpContext.RequestAborted);

        return Ok(ApiResult<object>.Success(new { batchId = id, status = CfBatchStatus.Processing, version = batch.FChangeVersion }, $"已开始重试（模式：{mode}）"));
    }

    #endregion

    #region 回收站与撤销

    /// <summary>
    /// 获取已撤销（回收站）批次列表
    /// </summary>
    [HttpGet("batches/recycled")]
    [RequirePermission(CardFlowPermissions.ImportUpload)]
    public async Task<IActionResult> GetRecycledBatches()
    {
        try
        {
            var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
            var list = await _context.Set<CfBatch>()
                .AsNoTracking()
                .Where(b => b.FIsRevoked && b.FOrgId == currentOrgId)
                .OrderByDescending(b => b.FRevokedTime)
                .Select(b => new
                {
                    Id = b.FID,
                    BatchNo = b.FBatchNo,
                    FileName = b.FFileName,
                    FileSize = b.FFileSize,
                    TotalRows = b.FTotalRows,
                    Status = b.FStatus,
                    RevokedTime = b.FRevokedTime,
                    RevokedById = b.FRevokedById,
                    CreateTime = b.FCreatedTime,
                    OrgId = b.FOrgId
                })
                .ToListAsync();

            return Ok(ApiResult<object>.Success(list));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取回收站批次列表失败");
            return Ok(ApiResult<object>.Fail($"获取失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 恢复已撤销的批次
    /// TODO: Pipeline废除 - BatchRevokeHandler 仍操作 DcImportBatch，待 Task 7 一并改造
    /// </summary>
    [HttpPost("batches/{id:long}/restore")]
    [RequirePermission(CardFlowPermissions.ImportProcess)]
    public async Task<IActionResult> RestoreBatch(long id)
    {
        try
        {
            var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
            // 直接在 CfBatch 上执行恢复
            var batch = await _context.Set<CfBatch>()
                .AsTracking()
                .FirstOrDefaultAsync(b => b.FID == id && b.FOrgId == currentOrgId);

            if (batch == null)
                return Ok(ApiResult<object>.Fail("批次不存在", 404));

            if (!batch.FIsRevoked)
                return Ok(ApiResult<object>.Fail("该批次未被撤销，无需恢复"));

            var operatorId = GetCurrentUserId();

            batch.FIsRevoked = false;
            batch.FRevokedTime = null;
            batch.FRevokedById = null;
            batch.FStatus = CfBatchStatus.Staged; // 恢复后回到已暂存状态
            batch.FUpdatedTime = DateTime.Now;
            await _context.SaveChangesAsync();

            // 版本号递增
            try
            {
                var versions = await _context.Database
                    .SqlQueryRaw<long>(
                        "UPDATE [CF批次] SET [F变更版本号] = NEXT VALUE FOR dbo.SEQ_BatchChange OUTPUT INSERTED.[F变更版本号] WHERE [FID] = {0}",
                        batch.FID)
                    .ToListAsync();
                batch.FChangeVersion = versions.FirstOrDefault();
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "版本号递增失败: BatchId={BatchId}, OrgId={OrgId}", batch.FID, currentOrgId);
            }

            return Ok(ApiResult.Ok("恢复成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复批次失败: {BatchId}", id);
            return Ok(ApiResult<object>.Fail($"恢复失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 批次删除预检查
    /// TODO: Pipeline废除 - BatchRevokeHandler.PreCheckAsync 仍操作 DcImportBatch，待改造
    /// </summary>
    [HttpGet("batches/{id:long}/pre-delete-check")]
    [RequirePermission(CardFlowPermissions.ImportProcess)]
    public async Task<ApiResult<BatchDeletePreCheck>> PreDeleteCheck(long id)
    {
        try
        {
            var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
            var batch = await _context.Set<CfBatch>().AsNoTracking().FirstOrDefaultAsync(b => b.FID == id && b.FOrgId == currentOrgId);
            if (batch == null)
                return ApiResult<BatchDeletePreCheck>.Fail("批次不存在", 404);

            var result2 = new BatchDeletePreCheck { CanDelete = true };

            // 注：不再阻止 Processing 状态的撤销操作
            // 撤销(软删除)是安全的，pipeline 会自行检测已撤销状态并停止

            if (batch.FIsRevoked)
                return ApiResult<BatchDeletePreCheck>.Success(result2); // 已撤销批次可以彻底删除

            // 检查关联凭证
            await CheckBatchVouchersAsync(id, result2);

            return ApiResult<BatchDeletePreCheck>.Success(result2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批次预检查失败: {BatchId}", id);
            return ApiResult<BatchDeletePreCheck>.Fail($"预检查失败：{ex.Message}", 500);
        }
    }

    /// <summary>
    /// 删除批次记录及其关联数据
    /// mode=revoke + force=false: 软删除（标记撤销）
    /// mode=revoke + force=true 或 mode=delete: 物理删除（级联删除）
    /// </summary>
    [HttpDelete("batches/{id:long}")]
    [RequirePermission(CardFlowPermissions.ImportProcess)]
    public async Task<ApiResult<BatchDeleteResult>> DeleteBatch(long id, [FromQuery] string mode = "revoke", [FromQuery] bool force = false)
    {
        try
        {
            var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
            var batch = await _context.Set<CfBatch>().AsNoTracking().FirstOrDefaultAsync(b => b.FID == id && b.FOrgId == currentOrgId);
            if (batch == null)
                return ApiResult<BatchDeleteResult>.Fail("批次不存在", 404);

            var operatorId = GetCurrentUserId();

            if (mode == "revoke" && !force)
            {
                // 软删除模式：标记 CfBatch.FIsRevoked
                await RevokeCfBatchAsync(id, operatorId);
            }
            else
            {
                // 物理删除模式
                await CascadeDeleteBatchAsync(id);
            }

            return ApiResult<BatchDeleteResult>.Success(new BatchDeleteResult { Success = true }, "批次已处理");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BatchDeleteResult>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除批次失败: {BatchId}", id);
            return ApiResult<BatchDeleteResult>.Fail($"删除失败：{ex.Message}", 500);
        }
    }

    /// <summary>
    /// 批量撤销多个批次（软删除）
    /// </summary>
    [HttpPost("batches/batch-revoke")]
    [RequirePermission(CardFlowPermissions.ImportProcess)]
    public async Task<ApiResult<BatchRevokeResult>> BatchRevoke([FromBody] BatchRevokeRequest request)
    {
        var result = new BatchRevokeResult();

        if (request.BatchIds == null || request.BatchIds.Count == 0)
            return ApiResult<BatchRevokeResult>.Success(result);

        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
        var operatorId = GetCurrentUserId();

        var batches = await _context.Set<CfBatch>()
            .Where(b => request.BatchIds.Contains(b.FID) && b.FOrgId == currentOrgId)
            .ToListAsync();

        var batchMap = batches.ToDictionary(b => b.FID);

        foreach (var batchId in request.BatchIds)
        {
            if (!batchMap.TryGetValue(batchId, out var batch))
            {
                result.Skipped.Add(new BatchRevokeSkipped { Id = batchId, Reason = "批次不存在" });
                continue;
            }

            if (batch.FIsRevoked)
            {
                result.Skipped.Add(new BatchRevokeSkipped { Id = batchId, Reason = "已在回收站" });
                continue;
            }

            try
            {
                await RevokeCfBatchAsync(batchId, operatorId);
                result.Succeeded.Add(batchId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "批量撤销中单个批次失败: BatchId={BatchId}", batchId);
                result.Skipped.Add(new BatchRevokeSkipped { Id = batchId, Reason = ex.Message });
            }
        }

        return ApiResult<BatchRevokeResult>.Success(result);
    }

    /// <summary>
    /// 清空回收站（彻底删除所有已撤销批次）
    /// </summary>
    [HttpPost("batches/recycled/clear")]
    [RequirePermission(CardFlowPermissions.ImportProcess)]
    public async Task<ApiResult<ClearRecycleBinResult>> ClearRecycleBin()
    {
        var result = new ClearRecycleBinResult();
        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

        var recycledBatches = await _context.Set<CfBatch>()
            .Where(b => b.FIsRevoked && b.FOrgId == currentOrgId)
            .ToListAsync();

        if (recycledBatches.Count == 0)
            return ApiResult<ClearRecycleBinResult>.Success(result);

        foreach (var batch in recycledBatches)
        {
            try
            {
                await CascadeDeleteBatchAsync(batch.FID);
                result.DeletedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清空回收站中单个批次删除失败: BatchId={BatchId}", batch.FID);
                result.FailedCount++;
            }
        }

        return ApiResult<ClearRecycleBinResult>.Success(result);
    }

    #endregion

    #region 重新计费

    /// <summary>
    /// 重新计费（对已完成/部分完成/失败的批次执行PricingPlugin）
    /// </summary>
    [HttpPost("batches/{id:long}/recalculate")]
    [RequirePermission(CardFlowPermissions.ImportProcess)]
    public async Task<ApiResult> RecalculateBatch(long id)
    {
        var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
        var batch = await _context.Set<CfBatch>()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.FID == id && b.FOrgId == currentOrgId);

        if (batch == null)
            return ApiResult.Fail("批次不存在", 404);

        // 允许失败批次重新进入计费链，便于修复价格/成本配置后重跑。
        if (batch.FStatus != CfBatchStatus.Completed
            && batch.FStatus != CfBatchStatus.PartiallyCompleted
            && batch.FStatus != CfBatchStatus.Failed)
            return ApiResult.Fail("仅已完成、部分完成或失败状态的批次可重新计费");

        var restartOrder = await GetStageOrderBeforeBillingAsync(batch.FFlowDefinitionId, HttpContext.RequestAborted);
        if (!restartOrder.HasValue)
            return ApiResult.Fail("当前流程未配置价格或成本计算节点，无法重新计费");

        // 更新状态为"处理中"
        batch.FStatus = CfBatchStatus.Processing;
        batch.FErrorMessage = null;
        batch.FCurrentBatchStageOrder = restartOrder.Value;
        batch.FCurrentNodeName = "重新计费";
        batch.FUpdatedTime = DateTime.Now;
        await _context.SaveChangesAsync();

        // 通知前端状态变更
        try
        {
            await _progressNotifier.NotifyBatchStatusChangedAsync(id, CfBatchStatus.Processing, "Processing");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "推送状态变更失败: BatchId={BatchId}, OrgId={OrgId}", id, currentOrgId);
        }

        // 后台通过 BatchTriggerService 执行批次级节点链（重跑计费节点）
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var triggerService = scope.ServiceProvider.GetRequiredService<IBatchTriggerService>();
                await triggerService.ProcessBatchJobAsync(
                    new BatchJob(id, BatchJobKind.ProcessBatchStages), CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重新计费批次 {BatchId} 失败", id);
            }
        });

        return ApiResult.Ok("已开始重新计费");
    }

    private async Task<int?> GetStageOrderBeforeBillingAsync(long flowDefinitionId, CancellationToken ct)
    {
        var version = await _context.Set<CfFlowVersion>()
            .AsNoTracking()
            .Where(v => v.FFlowDefinitionId == flowDefinitionId && v.FIsCurrentVersion)
            .OrderByDescending(v => v.FVersionNumber)
            .FirstOrDefaultAsync(ct);
        if (version == null) return null;

        var firstBillingOrder = await _context.Set<CfStageDefinition>()
            .AsNoTracking()
            .Join(_context.Set<CfAutoPluginRegistry>().AsNoTracking(),
                s => s.F插件注册ID,
                r => (long?)r.FID,
                (s, r) => new { Stage = s, Registry = r })
            .Where(x => x.Stage.FFlowVersionId == version.FID
                        && (x.Registry.F插件编码 == "Pricing" || x.Registry.F插件编码 == "Cost"))
            .Select(x => (int?)x.Stage.FSortOrder)
            .MinAsync(ct);

        return firstBillingOrder.HasValue ? Math.Max(0, firstBillingOrder.Value - 1) : null;
    }

    #endregion

    #region 组织绑定预检

    /// <summary>
    /// 组织绑定预检：校验目标组织与账套绑定
    /// </summary>
    [HttpPost("preview-org-binding")]
    [RequirePermission(CardFlowPermissions.UploadCenter)]
    public async Task<ApiResult<OrgBindingPreview>> PreviewOrgBinding([FromBody] OrgBindingPreviewRequest request)
    {
        var warnings = new List<string>();

        var org = await _context.Set<SysOrganization>()
            .AsNoTracking()
            .Where(o => o.FID == request.TargetOrgId)
            .Select(o => new { o.FID, o.FName, o.FStatus })
            .FirstOrDefaultAsync();

        if (org == null)
            return ApiResult<OrgBindingPreview>.Fail("目标组织不存在", 404);

        if (org.FStatus != 1)
            warnings.Add("目标组织已停用，可能影响数据导入");

        var accountSet = await _context.Set<FinAccountSet>()
            .AsNoTracking()
            .Where(a => a.FOrgId == request.TargetOrgId && a.FStatus == 1)
            .OrderByDescending(a => a.FIsDefault)
            .Select(a => new { a.FID, a.FName })
            .FirstOrDefaultAsync();

        if (accountSet == null)
            warnings.Add("该组织未绑定账套，导入后将无法自动生成凭证");

        var result = new OrgBindingPreview
        {
            TargetOrgId = org.FID,
            TargetOrgName = org.FName,
            ResolvedAccountSetId = accountSet?.FID,
            ResolvedAccountSetName = accountSet?.FName,
            Warnings = warnings.ToArray()
        };

        return ApiResult<OrgBindingPreview>.Success(result);
    }

    #endregion

    #region 异常数据与派发管理

    /// <summary>
    /// 异常数据查询（分页+筛选）
    /// </summary>
    [HttpGet("errors")]
    [RequirePermission(CardFlowPermissions.DispatchManage)]
    public async Task<ApiResult<PagedResult<ImportErrorDto>>> GetImportErrors([FromQuery] ImportErrorQueryDto query)
    {
        try
        {
            var result = await _dispatchService.GetImportErrorsAsync(query);
            return ApiResult<PagedResult<ImportErrorDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询异常数据失败");
            return ApiResult<PagedResult<ImportErrorDto>>.Fail($"查询失败：{ex.Message}", 500);
        }
    }

    /// <summary>
    /// 批量忽略错误
    /// </summary>
    [HttpPut("errors/ignore")]
    [RequirePermission(CardFlowPermissions.DispatchManage)]
    public async Task<ApiResult<int>> BatchIgnoreErrors([FromBody] BatchIgnoreErrorsRequest request)
    {
        try
        {
            var count = await _dispatchService.BatchIgnoreErrorsAsync(request.ErrorIds);
            return ApiResult<int>.Success(count, $"已忽略 {count} 条错误");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量忽略错误失败");
            return ApiResult<int>.Fail($"操作失败：{ex.Message}", 500);
        }
    }

    /// <summary>
    /// 导入总览统计
    /// </summary>
    [HttpGet("overview")]
    [RequirePermission(CardFlowPermissions.DispatchManage)]
    public async Task<ApiResult<ImportOverviewDto>> GetImportOverview()
    {
        try
        {
            var result = await _dispatchService.GetImportOverviewAsync();
            return ApiResult<ImportOverviewDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取导入总览失败");
            return ApiResult<ImportOverviewDto>.Fail($"获取失败：{ex.Message}", 500);
        }
    }

    /// <summary>
    /// 创建派发记录
    /// </summary>
    [HttpPost("dispatch")]
    [RequirePermission(CardFlowPermissions.DispatchManage)]
    public async Task<ApiResult<BusinessDispatchRecordDto>> CreateDispatch([FromBody] CreateDispatchDto dto)
    {
        try
        {
            var operatorName = User.Identity?.Name ?? "未知用户";
            var result = await _dispatchService.CreateDispatchAsync(dto, operatorName);
            return ApiResult<BusinessDispatchRecordDto>.Success(result, "派发创建成功");
        }
        catch (ArgumentException ex)
        {
            return ApiResult<BusinessDispatchRecordDto>.Fail(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BusinessDispatchRecordDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建派发失败");
            return ApiResult<BusinessDispatchRecordDto>.Fail($"创建失败：{ex.Message}", 500);
        }
    }

    /// <summary>
    /// 查询派发记录（分页+筛选）
    /// </summary>
    [HttpGet("dispatch")]
    [RequirePermission(CardFlowPermissions.DispatchManage)]
    public async Task<ApiResult<PagedResult<BusinessDispatchRecordDto>>> GetDispatchRecords(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] long? batchId = null)
    {
        try
        {
            var result = await _dispatchService.GetDispatchRecordsAsync(page, pageSize, status, batchId);
            return ApiResult<PagedResult<BusinessDispatchRecordDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询派发记录失败");
            return ApiResult<PagedResult<BusinessDispatchRecordDto>>.Fail($"查询失败：{ex.Message}", 500);
        }
    }

    /// <summary>
    /// 更新派发状态
    /// </summary>
    [HttpPut("dispatch/{id:long}")]
    [RequirePermission(CardFlowPermissions.DispatchManage)]
    public async Task<ApiResult<BusinessDispatchRecordDto>> UpdateDispatchStatus(long id, [FromBody] UpdateDispatchDto dto)
    {
        try
        {
            var result = await _dispatchService.UpdateDispatchStatusAsync(id, dto.Status, dto.Result);
            if (result == null)
                return ApiResult<BusinessDispatchRecordDto>.Fail("派发记录不存在", 404);
            return ApiResult<BusinessDispatchRecordDto>.Success(result, "状态更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新派发状态失败: {Id}", id);
            return ApiResult<BusinessDispatchRecordDto>.Fail($"更新失败：{ex.Message}", 500);
        }
    }

    #endregion

    #region 流程定义指定

    /// <summary>
    /// 手动为批次指定流程定义并开始处理（原 assign-pipeline，Pipeline废除后改用 FlowDefinition）
    /// </summary>
    [HttpPost("batches/{batchId:long}/assign-pipeline")]
    [RequirePermission(CardFlowPermissions.ImportProcess)]
    public async Task<ApiResult> AssignPipeline(long batchId, [FromBody] AssignPipelineDto dto)
    {
        try
        {
            var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
            var batch = await _context.Set<CfBatch>()
                .AsTracking()
                .FirstOrDefaultAsync(b => b.FID == batchId && b.FOrgId == currentOrgId);
            if (batch == null)
                return ApiResult.Fail("批次不存在", 404);

            // 使用 FlowDefinitionId
            var flowDefinitionId = dto.FlowDefinitionId;

            // 验证流程定义存在
            var flowDef = await _context.Set<CfFlowDefinition>()
                .FirstOrDefaultAsync(fd => fd.FID == flowDefinitionId);
            if (flowDef == null)
                return ApiResult.Fail("流程定义不存在", 404);

            batch.FFlowDefinitionId = flowDefinitionId ?? 0;
            batch.FImportStartTime = DateTime.Now;
            batch.FStatus = CfBatchStatus.Processing;
            batch.FUpdatedTime = DateTime.Now;
            await _context.SaveChangesAsync();

            // 版本号递增
            long version = 0;
            try
            {
                var versions = await _context.Database
                    .SqlQueryRaw<long>(
                        "UPDATE [CF批次] SET [F变更版本号] = NEXT VALUE FOR dbo.SEQ_BatchChange OUTPUT INSERTED.[F变更版本号] WHERE [FID] = {0}",
                        batchId)
                    .ToListAsync();
                version = versions.FirstOrDefault();
                batch.FChangeVersion = version;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "版本号递增失败: BatchId={BatchId}, OrgId={OrgId}", batchId, currentOrgId);
            }

            // 推送状态变更
            try
            {
                await _progressNotifier.NotifyBatchStatusChangedAsync(batchId, CfBatchStatus.Processing, "Processing");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "推送状态变更失败: BatchId={BatchId}, OrgId={OrgId}", batchId, currentOrgId);
            }

            // 通过 BatchTriggerService 触发执行
            using var scope = _serviceScopeFactory.CreateScope();
            var triggerService = scope.ServiceProvider.GetRequiredService<IBatchTriggerService>();
            await triggerService.ProcessBatchJobAsync(
                new BatchJob(batchId, BatchJobKind.ProcessBatchStages), HttpContext.RequestAborted);

            return new ApiResult { Code = 200, Message = "已开始处理", Data = new { batchId, status = CfBatchStatus.Processing, version } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "手动指定流程定义失败: BatchId={BatchId}", batchId);
            return ApiResult.Fail($"操作失败：{ex.Message}", 500);
        }
    }

    /// <summary>
    /// 获取可选流程定义列表（匹配失败时供前端选择）
    /// </summary>
    [HttpGet("flow-definition-candidates")]
    [RequirePermission(CardFlowPermissions.UploadCenter)]
    public async Task<ApiResult<List<FlowDefinitionCandidateDto>>> GetFlowDefinitionCandidates()
    {
        try
        {
            var currentOrgId = (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
            var candidates = await _batchTriggerService.GetFlowDefinitionCandidatesAsync(currentOrgId);
            return ApiResult<List<FlowDefinitionCandidateDto>>.Success(candidates);
        }
        catch (Exception)
        {
            // 查询异常时返回空列表，避免未处理异常
            return ApiResult<List<FlowDefinitionCandidateDto>>.Success(new List<FlowDefinitionCandidateDto>());
        }
    }

    #endregion

    #region 私有辅助方法

    private long GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(global::System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdStr) && long.TryParse(userIdStr, out var userId))
            return userId;
        return 0;
    }

    /// <summary>生成批次号</summary>
    private static string GenerateBatchNo()
    {
        var now = DateTime.Now;
        var second = now.ToString("yyyyMMdd-HHmmss");
        int seq;
        lock (_batchLock)
        {
            seq = ++_batchSeq;
        }
        return $"B{second}-{seq:D3}";
    }
    private static int _batchSeq;
    private static readonly object _batchLock = new();

    /// <summary>获取上传目录</summary>
    private string GetUploadBaseDir()
    {
        try
        {
            var path = _configuration.GetValue<string>("CardFlow:UploadPath");
            if (!string.IsNullOrWhiteSpace(path)) return path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取配置 CardFlow:UploadPath 失败");
        }

        try
        {
            var path = _configuration.GetValue<string>("DataCenter:UploadPath");
            if (!string.IsNullOrWhiteSpace(path)) return path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取配置 DataCenter:UploadPath 失败");
        }

        return "uploads/cardflow";
    }

    /// <summary>判断 CfBatch 是否正在处理中且未卡住</summary>
    private static bool IsCfBatchProcessingAndNotStale(CfBatch batch)
    {
        var isProcessing = batch.FStatus == CfBatchStatus.Processing;
        if (!isProcessing) return false;

        if (!string.IsNullOrEmpty(batch.FErrorMessage))
            return false; // 有错误，允许撤销

        var startTime = batch.FImportStartTime ?? batch.FCreatedTime;
        if ((DateTime.Now - startTime).TotalMinutes > 10)
            return false; // 已卡住，允许撤销

        return true;
    }

    /// <summary>判断 CfBatch 是否已卡住</summary>
    private static bool IsCfBatchStale(CfBatch batch)
    {
        if (batch.FStatus == CfBatchStatus.Processing)
        {
            var startTime = batch.FImportStartTime ?? batch.FCreatedTime;
            if ((DateTime.Now - startTime).TotalMinutes > 10)
                return true;
        }
        return false;
    }

    /// <summary>将 CfBatch 映射为 ImportBatchDto</summary>
    private ImportBatchDto MapCfBatchToDto(CfBatch batch)
    {
        return new ImportBatchDto
        {
            Id = batch.FID,
            BatchNo = batch.FBatchNo ?? string.Empty,
            FileName = batch.FFileName,
            FileSize = batch.FFileSize ?? 0,
            TotalRows = batch.FTotalRows,
            SuccessRows = batch.FSuccessRows,
            FailRows = batch.FFailedRows,
            SkipRows = batch.FSkipRows,
            Status = batch.FStatus,
            UploadMethod = batch.FUploadMethod,
            ImportStartTime = batch.FImportStartTime,
            ImportEndTime = batch.FImportEndTime,
            CreateTime = batch.FCreatedTime,
            ErrorSummary = batch.FErrorMessage,
            ActualTargetTable = batch.FActualTargetTable,
            CurrentStepName = batch.FCurrentNodeName,
            CurrentPhase = batch.FCurrentNodeName,
            ProcessedRows = batch.FSuccessRows + batch.FFailedRows + batch.FSkipRows,
            ProgressPercent = batch.FProgressPercent,
            IsStale = IsCfBatchStale(batch),
            Version = batch.FChangeVersion,
        };
    }

    /// <summary>软删除 CfBatch（标记撤销）</summary>
    private async Task RevokeCfBatchAsync(long batchId, long operatorId)
    {
        var batch = await _context.Set<CfBatch>()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.FID == batchId)
            ?? throw new InvalidOperationException($"批次 {batchId} 不存在");

        // 注：不再阻止 Processing 状态的撤销（软删除安全，pipeline 会自行检测已撤销状态并停止）

        if (batch.FIsRevoked)
        {
            _logger.LogInformation("批次 {BatchId} 已处于撤销状态，重复撤销请求被忽略", batchId);
            return;
        }

        batch.FIsRevoked = true;
        batch.FRevokedTime = DateTime.Now;
        batch.FRevokedById = operatorId;
        batch.FStatus = CfBatchStatus.Revoked;
        batch.FUpdatedTime = DateTime.Now;
        await _context.SaveChangesAsync();

        // 版本号递增
        try
        {
            var versions = await _context.Database
                .SqlQueryRaw<long>(
                    "UPDATE [CF批次] SET [F变更版本号] = NEXT VALUE FOR dbo.SEQ_BatchChange OUTPUT INSERTED.[F变更版本号] WHERE [FID] = {0}",
                    batchId)
                .ToListAsync();
            batch.FChangeVersion = versions.FirstOrDefault();
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "版本号递增失败: BatchId={BatchId}", batchId);
        }

        // 推送状态变更
        try
        {
            await _progressNotifier.NotifyBatchStatusChangedAsync(batchId, CfBatchStatus.Revoked, "Revoked");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "推送状态变更失败: BatchId={BatchId}", batchId);
        }
    }

    /// <summary>检查批次关联凭证状态</summary>
    private async Task CheckBatchVouchersAsync(long batchId, BatchDeletePreCheck result)
    {
        var encryptionKey = _configuration.GetValue<string>("Security:EncryptionKey");
        var connectionString = DbConnectionsHelper.GetSystemConnectionString(encryptionKey);
        if (string.IsNullOrEmpty(connectionString)) return;

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // 从 CF凭证记录 和 FIN凭证 查询关联凭证（DC凭证生成记录 已替换为 CF凭证记录）
            var scopeId = batchId.ToString();
            int voucherCount = 0;
            // 3a. 从 CF凭证记录 查询批次关联的凭证生成记录
            using (var cmd = new SqlCommand(
                "SELECT ISNULL(SUM([F生成凭证数]), 0) FROM [CF凭证记录] WHERE [F批次ID] = @batchId",
                connection))
            {
                cmd.Parameters.AddWithValue("@batchId", batchId);
                cmd.CommandTimeout = 60;
                voucherCount = Math.Max(voucherCount, (int)(await cmd.ExecuteScalarAsync() ?? 0));
            }

            // 3b. 从 FIN凭证 按 F数据作用域ID 查询实际凭证数
            using (var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM [FIN凭证] WHERE [F数据作用域ID] = @scopeId",
                connection))
            {
                cmd.Parameters.AddWithValue("@scopeId", scopeId);
                cmd.CommandTimeout = 60;
                voucherCount = Math.Max(voucherCount, (int)(await cmd.ExecuteScalarAsync() ?? 0));
            }

            result.AffectedVoucherCount = voucherCount;

            if (voucherCount > 0)
            {
                int auditedCount;
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM [FIN凭证] WHERE [F数据作用域ID] = @scopeId AND [F状态] = 2",
                    connection))
                {
                    cmd.Parameters.AddWithValue("@scopeId", scopeId);
                    cmd.CommandTimeout = 60;
                    auditedCount = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                }

                if (auditedCount > 0)
                {
                    result.HasAuditedVouchers = true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查批次 {BatchId} 关联凭证失败", batchId);
        }
    }

    /// <summary>级联清理子批次的 STG 数据</summary>
    // TODO: Pipeline废除 - 待确认 CfBatch 是否有父子批次概念，目前暂保留原逻辑
    private async Task CleanChildBatchesAsync(long parentBatchId)
    {
        // CfBatch 当前无 F父批次ID 字段，暂不清理子批次
        await Task.CompletedTask;
    }

    #endregion

    #region 级联删除（物理删除）

    private async Task<BatchDeleteResult> CascadeDeleteBatchAsync(long batchId)
    {
        var result = new BatchDeleteResult { Success = true };
        var encryptionKey = _configuration.GetValue<string>("Security:EncryptionKey");
        var connectionString = DbConnectionsHelper.GetSystemConnectionString(encryptionKey)
            ?? throw new InvalidOperationException("无法获取数据库连接字符串");

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = (SqlTransaction)connection.BeginTransaction();

        try
        {
            // Step 0: 获取批次状态（检查是否正在处理中）
            int batchStatus = -1;
            try
            {
                using var cmdScope = new SqlCommand(
                    "SELECT [F状态] FROM [CF批次] WITH (UPDLOCK) WHERE [FID] = @batchId",
                    connection, transaction);
                cmdScope.Parameters.AddWithValue("@batchId", batchId);
                cmdScope.CommandTimeout = 30;
                using var scopeReader = await cmdScope.ExecuteReaderAsync();
                if (await scopeReader.ReadAsync())
                {
                    batchStatus = scopeReader.GetInt32(0);
                }
                scopeReader.Close();

                // 注：不再阻止 Processing 状态的物理删除（用户明确操作时应允许）
            }
            catch (InvalidOperationException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取批次 {BatchId} 的状态失败", batchId);
            }

            // 获取 FActualTargetTable
            string? actualTargetTable = null;
            try
            {
                using var cmdTable = new SqlCommand(
                    "SELECT [F实际暂存表] FROM [CF批次] WHERE [FID] = @batchId",
                    connection, transaction);
                cmdTable.Parameters.AddWithValue("@batchId", batchId);
                cmdTable.CommandTimeout = 30;
                using var tableReader = await cmdTable.ExecuteReaderAsync();
                if (await tableReader.ReadAsync())
                    actualTargetTable = tableReader.IsDBNull(0) ? null : tableReader.GetString(0);
                tableReader.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取批次 {BatchId} 的暂存表名失败", batchId);
            }

            if (!string.IsNullOrEmpty(actualTargetTable))
            {
                await BatchDeleteAsync(connection, transaction,
                    "EXP出港运单_计费结果_成本明细",
                    "[F计费结果ID] IN (SELECT br.FID FROM [EXP出港运单_计费结果] br WHERE br.[F批次ID] = @batchId)",
                    new SqlParameter[] { new SqlParameter("@batchId", batchId) });

                await BatchDeleteAsync(connection, transaction,
                    "EXP出港运单_计费结果",
                    "[F批次ID] = @batchId AND [F账单ID] IS NULL",
                    new SqlParameter[] { new SqlParameter("@batchId", batchId) });
            }

            await CascadeDeleteVouchersAsync(connection, transaction, batchId, actualTargetTable, result, force: false);

            await ExecuteDeleteAsync(connection, transaction,
                "DELETE FROM [FIN凭证资产关联] WHERE [F批次ID] = @batchId",
                batchId);

            await ExecuteDeleteAsync(connection, transaction,
                "DELETE FROM [CF批次错误] WHERE [F批次ID] = @batchId",
                batchId);

            await ExecuteDeleteAsync(connection, transaction,
                "DELETE FROM [CF业务派发记录] WHERE [F批次ID] = @batchId",
                batchId);

            await ExecuteDeleteAsync(connection, transaction,
                "DELETE FROM [CF系统派发结果] WHERE [F批次ID] = @batchId",
                batchId);

            await ExecuteDeleteAsync(connection, transaction,
                "DELETE FROM [CF凭证记录] WHERE [F批次ID] = @batchId",
                batchId);

            if (!string.IsNullOrEmpty(actualTargetTable))
            {
                await BatchDeleteAsync(connection, transaction,
                    actualTargetTable,
                    "[F批次ID] = @batchId",
                    new SqlParameter[] { new SqlParameter("@batchId", batchId) });
            }

            // 清理工作项 — F模块 从 'DataCenter' 改为 'CardFlow'
            try
            {
                using var cmdWorkItem = new SqlCommand(
                    "DELETE FROM [WF工作项] WHERE [F模块] = 'CardFlow' AND [F业务类型] = 'ImportBatch' AND [F业务ID] = @batchId",
                    connection, transaction);
                cmdWorkItem.Parameters.AddWithValue("@batchId", batchId);
                cmdWorkItem.CommandTimeout = 120;
                await cmdWorkItem.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理批次 {BatchId} 关联工作项失败", batchId);
            }

            // 删除 CfBatch 行 — 从 [DC导入批次] 改为 [CF批次]
            await ExecuteDeleteAsync(connection, transaction,
                "DELETE FROM [CF批次] WHERE [FID] = @batchId",
                batchId);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return result;
    }

    private async Task CascadeDeleteVouchersAsync(
        SqlConnection connection, SqlTransaction transaction,
        long batchId, string? actualTargetTable, BatchDeleteResult result, bool force = false)
    {
        var auditedIds = new List<long>();
        var unauditedIds = new List<long>();

        // AutoVoucherHandler 创建凭证时 DataScopeId 有三种模式：
        //   1. businessKey:  "41|F网点编号=TC001|F费用类别=快递费"  （keyFields 有配置时）
        //   2. 复合键:       "41_rg1_20260413"                     （GroupBy 为空时）
        //   3. 纯批次ID:     "41"                                  （GroupBy 非空且无 keyFields 时）
        // VoucherMigrationPlugin 创建的凭证 DataScopeId = 纯批次ID
        // 因此需用 LIKE 模式覆盖全部场景，[_] 转义下划线以防 SQL 通配符误匹配
        var batchIdStr = batchId.ToString();
        var batchIdPipe = batchIdStr + "|%";       // 匹配模式1：businessKey
        var batchIdUnderscore = batchIdStr + "[_]%"; // 匹配模式2：复合键

        using (var cmd = new SqlCommand(
            @"SELECT [FID], [F状态] FROM [FIN凭证]
               WHERE [F数据作用域ID] = @batchId
                  OR [F数据作用域ID] LIKE @batchIdPipe
                  OR [F数据作用域ID] LIKE @batchIdUnderscore",
            connection, transaction))
        {
            cmd.Parameters.AddWithValue("@batchId", batchIdStr);
            cmd.Parameters.AddWithValue("@batchIdPipe", batchIdPipe);
            cmd.Parameters.AddWithValue("@batchIdUnderscore", batchIdUnderscore);
            cmd.CommandTimeout = 60;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var vid = reader.GetInt64(0);
                var status = reader.GetInt32(1);
                if (status == 2) auditedIds.Add(vid);
                else unauditedIds.Add(vid);
            }
        }

        if (auditedIds.Count == 0 && unauditedIds.Count == 0) return;

        if (unauditedIds.Count > 0)
        {
            foreach (var batch in unauditedIds.Chunk(500))
            {
                var idList = string.Join(",", batch);
                using (var cmd = new SqlCommand(
                    $"DELETE FROM [FIN凭证分录] WHERE [F凭证ID] IN ({idList})",
                    connection, transaction))
                {
                    cmd.CommandTimeout = 120;
                    await cmd.ExecuteNonQueryAsync();
                }
                using (var cmd = new SqlCommand(
                    $"DELETE FROM [FIN凭证] WHERE [FID] IN ({idList}) AND [F状态] != 2",
                    connection, transaction))
                {
                    cmd.CommandTimeout = 120;
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            result.DeletedVoucherCount = unauditedIds.Count;
        }

        if (auditedIds.Count > 0)
        {
            if (force)
            {
                var deletableAuditedIds = new List<long>();
                var closedPeriodIds = new List<long>();

                foreach (var chunk in auditedIds.Chunk(500))
                {
                    var idList = string.Join(",", chunk);
                    using var cmd = new SqlCommand(
                        $@"SELECT v.[FID], ISNULL(p.[F是否结账], 0) AS ClosedStatus
                           FROM [FIN凭证] v
                           LEFT JOIN [FIN会计期间] p ON p.[FID] = v.[F期间ID]
                           WHERE v.[FID] IN ({idList})",
                        connection, transaction);
                    cmd.CommandTimeout = 60;
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var vid = reader.GetInt64(0);
                        var closedStatus = reader.GetInt32(1);
                        if (closedStatus == 1) closedPeriodIds.Add(vid);
                        else deletableAuditedIds.Add(vid);
                    }
                }

                if (deletableAuditedIds.Count > 0)
                {
                    foreach (var chunk in deletableAuditedIds.Chunk(500))
                    {
                        var idList = string.Join(",", chunk);
                        using (var cmd = new SqlCommand(
                            $"DELETE FROM [FIN凭证分录] WHERE [F凭证ID] IN ({idList})",
                            connection, transaction))
                        {
                            cmd.CommandTimeout = 120;
                            await cmd.ExecuteNonQueryAsync();
                        }
                        using (var cmd = new SqlCommand(
                            $"DELETE FROM [FIN凭证] WHERE [FID] IN ({idList})",
                            connection, transaction))
                        {
                            cmd.CommandTimeout = 120;
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    result.DeletedVoucherCount += deletableAuditedIds.Count;
                }

                if (closedPeriodIds.Count > 0)
                {
                    result.RetainedVoucherCount = closedPeriodIds.Count;
                    result.RetainedVoucherIds = closedPeriodIds;
                    result.Warning = $"有 {closedPeriodIds.Count} 张已审核凭证位于已结账期间，未被删除";
                }
            }
            else
            {
                result.RetainedVoucherCount = auditedIds.Count;
                result.RetainedVoucherIds = auditedIds;
                result.Warning = $"有 {auditedIds.Count} 张已审核凭证未被删除（ID: {string.Join(", ", auditedIds.Take(10))}{(auditedIds.Count > 10 ? "..." : "")}）";
            }
        }
    }

    private static async Task BatchDeleteAsync(SqlConnection connection, SqlTransaction transaction,
        string tableName, string whereClause, SqlParameter[]? parameters = null, int batchSize = 5000)
    {
        int deleted;
        do
        {
            var sql = $"DELETE TOP ({batchSize}) FROM [{tableName}] WHERE {whereClause}";
            using var cmd = new SqlCommand(sql, connection, transaction);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.ParameterName, param.Value ?? DBNull.Value);
                }
            }
            cmd.CommandTimeout = 300;
            deleted = await cmd.ExecuteNonQueryAsync();
        } while (deleted == batchSize);
    }

    private static async Task ExecuteDeleteAsync(SqlConnection connection, SqlTransaction transaction,
        string sql, long batchId)
    {
        using var cmd = new SqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("@batchId", batchId);
        cmd.CommandTimeout = 120;
        await cmd.ExecuteNonQueryAsync();
    }

    #endregion
}
