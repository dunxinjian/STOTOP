using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Import;

namespace STOTOP.Module.CardFlow.Controllers;

/// <summary>
/// CardFlow 批次管理 API
/// </summary>
[Authorize]
[ApiController]
[Route("api/cardflow/batches")]
public class CardFlowBatchController : ControllerBase
{
    private readonly STOTOPDbContext _db;
    private readonly IBatchTriggerService _triggerService;
    private readonly IBatchLifecycleService _lifecycleService;
    private readonly IWebHostEnvironment _env;
    private readonly ExcelParserService _excelParser;

    public CardFlowBatchController(
        STOTOPDbContext db,
        IBatchTriggerService triggerService,
        IBatchLifecycleService lifecycleService,
        IWebHostEnvironment env,
        ExcelParserService excelParser)
    {
        _db = db;
        _triggerService = triggerService;
        _lifecycleService = lifecycleService;
        _env = env;
        _excelParser = excelParser;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>文件上传触发批次</summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ApiResult<object>> Upload(
        IFormFile file,
        [FromForm] long flowDefinitionId,
        [FromForm] string? columnMapping)
    {
        if (file == null || file.Length == 0)
            return ApiResult<object>.Fail("请上传文件");
        if (flowDefinitionId <= 0)
            return ApiResult<object>.Fail("flowDefinitionId 无效");

        var orgId = GetOrgId();
        if (orgId == 0)
            return ApiResult<object>.Fail("当前无组织上下文", 401);

        // 持久化文件到 uploads/cardflow
        var uploadDir = Path.Combine(_env.ContentRootPath, "uploads", "cardflow");
        Directory.CreateDirectory(uploadDir);
        var safeName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var savedPath = Path.Combine(uploadDir, safeName);
        await using (var fs = global::System.IO.File.Create(savedPath))
        {
            await file.CopyToAsync(fs);
        }

        Dictionary<string, string> mapping;
        try
        {
            mapping = string.IsNullOrWhiteSpace(columnMapping)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(columnMapping)
                  ?? new Dictionary<string, string>();
        }
        catch (JsonException ex)
        {
            return ApiResult<object>.Fail($"columnMapping 格式无效：{ex.Message}");
        }

        try
        {
            var batchId = await _triggerService.TriggerByFileUploadAsync(
                flowDefinitionId, orgId, GetUserId(), savedPath, mapping);

            return ApiResult<object>.Success(new { batchId, fileName = file.FileName }, "已触发批次解析");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 多文件内容路由批量上传：一次传多个文件，逐个落盘→读表头→按内容路由分类。
    /// routed（唯一命中）触发导入；unmatched（0 命中）进待认领；ambiguous（多命中）进多义。
    /// </summary>
    [HttpPost("upload-auto")]
    [Consumes("multipart/form-data")]
    public async Task<ApiResult<object>> UploadAuto(IFormFileCollection files)
    {
        if (files == null || files.Count == 0)
            return ApiResult<object>.Fail("请上传至少一个文件");

        var orgId = GetOrgId();
        if (orgId == 0)
            return ApiResult<object>.Fail("当前无组织上下文", 401);

        var uploadDir = Path.Combine(_env.ContentRootPath, "uploads", "cardflow");
        Directory.CreateDirectory(uploadDir);

        // 逐个落盘 + 读表头，建立 文件名→(列头, 落盘路径) 映射
        var savedPaths = new Dictionary<string, string>();        // fileName → savedPath
        var headers = new List<FileColumnHeader>();
        var readErrors = new List<object>();

        foreach (var file in files)
        {
            if (file == null || file.Length == 0) continue;

            var safeName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var savedPath = Path.Combine(uploadDir, safeName);
            await using (var fs = global::System.IO.File.Create(savedPath))
            {
                await file.CopyToAsync(fs);
            }

            try
            {
                await using var read = global::System.IO.File.OpenRead(savedPath);
                var cols = await _excelParser.ReadHeadersAsync(read, file.FileName, headerRow: 1);
                headers.Add(new FileColumnHeader(file.FileName, cols));
                savedPaths[file.FileName] = savedPath;
            }
            catch (Exception ex)
            {
                // 读表头失败（损坏/非表格）单独归类，不阻断其余文件
                readErrors.Add(new { fileName = file.FileName, error = ex.Message });
            }
        }

        // 内容路由分类（仅匹配，不导入）
        var classification = await _triggerService.ClassifyFilesAsync(headers, orgId);

        // routed 项触发导入（命中唯一流程）
        var routed = new List<object>();
        var triggerErrors = new List<object>();
        foreach (var r in classification.Routed)
        {
            if (!savedPaths.TryGetValue(r.FileName, out var savedPath)) continue;
            try
            {
                var batchId = await _triggerService.TriggerByFileUploadAsync(
                    r.FlowDefinitionId, orgId, GetUserId(), savedPath, new Dictionary<string, string>());
                routed.Add(new { fileName = r.FileName, batchId, flowDefinitionId = r.FlowDefinitionId });
            }
            catch (Exception ex)
            {
                // 触发导入失败（如持久化异常）单独归类，不阻断其余已命中文件，也不让整批 500
                triggerErrors.Add(new { fileName = r.FileName, flowDefinitionId = r.FlowDefinitionId, error = ex.Message });
            }
        }

        var unmatched = classification.Unmatched
            .Select(u => new { fileName = u.FileName, columns = u.Columns })
            .ToList();
        var ambiguous = classification.Ambiguous
            .Select(a => new
            {
                fileName = a.FileName,
                candidates = a.Candidates.Select(c => new { c.FlowDefinitionId, c.PluginRuleId }).ToList()
            })
            .ToList();

        return ApiResult<object>.Success(
            new { routed, unmatched, ambiguous, readErrors, triggerErrors },
            $"路由完成：触发 {routed.Count}，待认领 {unmatched.Count}，多义 {ambiguous.Count}，触发失败 {triggerErrors.Count}");
    }

    /// <summary>批次列表（分页 + 简易过滤）</summary>
    [HttpGet]
    public async Task<ApiResult<PagedResult<BatchListItemDto>>> GetList(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? flowDefinitionId = null,
        [FromQuery] int? status = null,
        [FromQuery] bool includeRevoked = false)
    {
        var query = _db.Set<CfBatch>().AsNoTracking().AsQueryable();
        if (!includeRevoked) query = query.Where(b => !b.FIsRevoked);
        if (flowDefinitionId.HasValue) query = query.Where(b => b.FFlowDefinitionId == flowDefinitionId.Value);
        if (status.HasValue) query = query.Where(b => b.FStatus == status.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.FID)
            .Skip((Math.Max(1, pageIndex) - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BatchListItemDto
            {
                Id = b.FID,
                FlowDefinitionId = b.FFlowDefinitionId,
                TriggerType = b.FTriggerType,
                TotalRows = b.FTotalRows,
                SuccessRows = b.FSuccessRows,
                FailedRows = b.FFailedRows,
                Status = b.FStatus,
                IsRevoked = b.FIsRevoked,
                TriggeredById = b.FTriggeredById,
                TriggeredTime = b.FTriggeredTime,
                FilePath = b.FFilePath,
                ErrorMessage = b.FErrorMessage,
                CreatedTime = b.FCreatedTime,
                UpdatedTime = b.FUpdatedTime
            })
            .ToListAsync();

        return ApiResult<PagedResult<BatchListItemDto>>.Success(new PagedResult<BatchListItemDto>
        {
            Items = items,
            Total = total,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    /// <summary>批次详情</summary>
    [HttpGet("{id:long}")]
    public async Task<ApiResult<BatchListItemDto>> GetById(long id)
    {
        var batch = await _db.Set<CfBatch>().AsNoTracking().FirstOrDefaultAsync(b => b.FID == id);
        if (batch == null) return ApiResult<BatchListItemDto>.Fail("批次不存在");

        return ApiResult<BatchListItemDto>.Success(new BatchListItemDto
        {
            Id = batch.FID,
            FlowDefinitionId = batch.FFlowDefinitionId,
            TriggerType = batch.FTriggerType,
            TotalRows = batch.FTotalRows,
            SuccessRows = batch.FSuccessRows,
            FailedRows = batch.FFailedRows,
            Status = batch.FStatus,
            IsRevoked = batch.FIsRevoked,
            TriggeredById = batch.FTriggeredById,
            TriggeredTime = batch.FTriggeredTime,
            FilePath = batch.FFilePath,
            ErrorMessage = batch.FErrorMessage,
            CreatedTime = batch.FCreatedTime,
            UpdatedTime = batch.FUpdatedTime
        });
    }

    /// <summary>批次进度</summary>
    [HttpGet("{id:long}/progress")]
    public async Task<ApiResult<BatchProgressDto>> GetProgress(long id)
    {
        try
        {
            var dto = await _lifecycleService.GetBatchProgressAsync(id);
            return ApiResult<BatchProgressDto>.Success(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BatchProgressDto>.Fail(ex.Message);
        }
    }

    /// <summary>批次明细列表（分页）</summary>
    [HttpGet("{id:long}/rows")]
    public async Task<ApiResult<PagedResult<BatchRowDto>>> GetRows(
        long id,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int? status = null)
    {
        var query = _db.Set<CfBatchRow>().AsNoTracking().Where(r => r.FBatchId == id);
        if (status.HasValue) query = query.Where(r => r.FStatus == status.Value);

        var total = await query.CountAsync();
        var rows = await query
            .OrderBy(r => r.FRowNo)
            .Skip((Math.Max(1, pageIndex) - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new BatchRowDto
            {
                Id = r.FID,
                BatchId = r.FBatchId,
                RowNo = r.FRowNo,
                DataJson = r.FDataJson,
                Status = r.FStatus,
                ErrorMessage = r.FErrorMessage,
                CardId = r.FCardId,
                RowVersion = Convert.ToBase64String(r.FRowVersion),
                CreatedTime = r.FCreatedTime,
                UpdatedTime = r.FUpdatedTime
            })
            .ToListAsync();

        return ApiResult<PagedResult<BatchRowDto>>.Success(new PagedResult<BatchRowDto>
        {
            Items = rows,
            Total = total,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    /// <summary>编辑暂存行（仅在批次 FStatus=1 已暂存 时允许；含乐观锁）</summary>
    [HttpPatch("{id:long}/rows/{rowId:long}")]
    public async Task<ApiResult<BatchRowDto>> UpdateRow(long id, long rowId, [FromBody] UpdateBatchRowRequest request)
    {
        var batch = await _db.Set<CfBatch>().FirstOrDefaultAsync(b => b.FID == id);
        if (batch == null) return ApiResult<BatchRowDto>.Fail("批次不存在");
        if (batch.FIsRevoked) return ApiResult<BatchRowDto>.Fail("批次已撤销，不可编辑");
        if (batch.FStatus != 1)
            return ApiResult<BatchRowDto>.Fail($"批次当前状态={batch.FStatus}，仅 1=已暂存 状态可编辑行");

        var row = await _db.Set<CfBatchRow>().FirstOrDefaultAsync(r => r.FID == rowId && r.FBatchId == id);
        if (row == null) return ApiResult<BatchRowDto>.Fail("行不存在");

        // 乐观锁校验
        if (!string.IsNullOrEmpty(request.RowVersion))
        {
            var current = Convert.ToBase64String(row.FRowVersion);
            if (!string.Equals(current, request.RowVersion, StringComparison.Ordinal))
                return ApiResult<BatchRowDto>.Fail("数据已被修改，请刷新后重试", 409);
        }

        if (request.DataJson != null)
        {
            // 校验 JSON 合法
            try { using var _ = JsonDocument.Parse(request.DataJson); }
            catch (JsonException) { return ApiResult<BatchRowDto>.Fail("dataJson 格式无效"); }
            row.FDataJson = request.DataJson;
        }

        // 编辑后回到 0=待质检 状态（除非显式设置）
        if (request.Status.HasValue) row.FStatus = request.Status.Value;
        else if (row.FStatus == 2) row.FStatus = 0; // 质检失败编辑后转为待质检
        row.FUpdatedTime = DateTime.Now;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResult<BatchRowDto>.Fail("数据已被修改，请刷新后重试", 409);
        }

        return ApiResult<BatchRowDto>.Success(new BatchRowDto
        {
            Id = row.FID,
            BatchId = row.FBatchId,
            RowNo = row.FRowNo,
            DataJson = row.FDataJson,
            Status = row.FStatus,
            ErrorMessage = row.FErrorMessage,
            CardId = row.FCardId,
            RowVersion = Convert.ToBase64String(row.FRowVersion),
            CreatedTime = row.FCreatedTime,
            UpdatedTime = row.FUpdatedTime
        }, "已保存");
    }

    /// <summary>排除行：状态置 4=已忽略</summary>
    [HttpPost("{id:long}/rows/exclude")]
    public async Task<ApiResult<object>> ExcludeRows(long id, [FromBody] BatchRowIdsRequest request)
    {
        var batch = await _db.Set<CfBatch>().FirstOrDefaultAsync(b => b.FID == id);
        if (batch == null) return ApiResult<object>.Fail("批次不存在");
        if (batch.FStatus != 1)
            return ApiResult<object>.Fail($"批次当前状态={batch.FStatus}，仅 1=已暂存 状态可排除行");

        var rows = await _db.Set<CfBatchRow>()
            .Where(r => r.FBatchId == id && request.RowIds.Contains(r.FID))
            .ToListAsync();

        foreach (var r in rows)
        {
            r.FStatus = 4;
            r.FUpdatedTime = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        return ApiResult<object>.Success(new { affected = rows.Count }, "已排除");
    }

    /// <summary>恢复已排除行：状态从 4=已忽略 → 0=待质检</summary>
    [HttpPost("{id:long}/rows/restore")]
    public async Task<ApiResult<object>> RestoreRows(long id, [FromBody] BatchRowIdsRequest request)
    {
        var batch = await _db.Set<CfBatch>().FirstOrDefaultAsync(b => b.FID == id);
        if (batch == null) return ApiResult<object>.Fail("批次不存在");
        if (batch.FStatus != 1)
            return ApiResult<object>.Fail($"批次当前状态={batch.FStatus}，仅 1=已暂存 状态可恢复行");

        var rows = await _db.Set<CfBatchRow>()
            .Where(r => r.FBatchId == id && r.FStatus == 4 && request.RowIds.Contains(r.FID))
            .ToListAsync();

        foreach (var r in rows)
        {
            r.FStatus = 0;
            r.FUpdatedTime = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        return ApiResult<object>.Success(new { affected = rows.Count }, "已恢复");
    }

    /// <summary>确认提交：触发质检 + fan-out</summary>
    [HttpPost("{id:long}/confirm")]
    public async Task<ApiResult<object>> Confirm(long id)
    {
        try
        {
            await _triggerService.ConfirmStagingAndFanOutAsync(id);
            return ApiResult<object>.Success(new { batchId = id }, "已确认提交，正在质检 + 创建卡片");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<object>.Fail(ex.Message);
        }
    }

    /// <summary>撤销批次（软删除 + 级联取消未完成卡片 + 凭证红冲）</summary>
    [HttpDelete("{id:long}")]
    public async Task<ApiResult<object>> Revoke(long id)
    {
        try
        {
            await _lifecycleService.RevokeBatchAsync(id, GetUserId());
            return ApiResult<object>.Success(new { batchId = id }, "已撤销");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<object>.Fail(ex.Message);
        }
    }
}

#region DTOs

public class BatchListItemDto
{
    public long Id { get; set; }
    public long FlowDefinitionId { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public int Status { get; set; }
    public bool IsRevoked { get; set; }
    public long TriggeredById { get; set; }
    public DateTime TriggeredTime { get; set; }
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? UpdatedTime { get; set; }
}

public class BatchRowDto
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public int RowNo { get; set; }
    public string DataJson { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? ErrorMessage { get; set; }
    public long? CardId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public DateTime? UpdatedTime { get; set; }
}

public class UpdateBatchRowRequest
{
    public string? DataJson { get; set; }
    public int? Status { get; set; }
    /// <summary>乐观锁版本号（Base64 编码的 byte[]）</summary>
    public string? RowVersion { get; set; }
}

public class BatchRowIdsRequest
{
    public List<long> RowIds { get; set; } = new();
}

#endregion
