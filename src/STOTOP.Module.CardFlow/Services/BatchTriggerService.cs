using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Services;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 后台批次任务（由 Channel 投递给 BatchJobProcessorService）
/// </summary>
public record BatchJob(long BatchId, BatchJobKind Kind);

public enum BatchJobKind
{
    /// <summary>解析 Excel 并写入 CfBatchRow（FStatus 0→1）</summary>
    ParseAndStage = 0,
    /// <summary>对已暂存批次执行质检并 fan-out 卡片（FStatus 2→3）</summary>
    QualityCheckAndFanOut = 1,
    /// <summary>批次级节点链处理：按 FSortOrder 顺序执行 batchAuto 节点（新流程统一入口）</summary>
    ProcessBatchStages = 2,
}

/// <summary>
/// 流程匹配结果（支持多流程同时命中）
/// </summary>
public record MatchResult(long FlowDefinitionId, long PluginRuleId);

/// <summary>
/// 多文件路由分类的输入项：一个文件的文件名 + 其列头集合。
/// </summary>
public record FileColumnHeader(string FileName, IReadOnlyList<string> Columns);

/// <summary>
/// 已唯一命中的文件（routed）：精确路由到一个流程。
/// </summary>
public record RoutedFile(string FileName, long FlowDefinitionId, long PluginRuleId);

/// <summary>
/// 未命中任何流程的文件（待认领）。
/// </summary>
public record UnmatchedFile(string FileName, IReadOnlyList<string> Columns);

/// <summary>
/// 命中多个流程的文件（多义）：需人工裁决选哪个流程。
/// </summary>
public record AmbiguousFile(string FileName, IReadOnlyList<MatchResult> Candidates);

/// <summary>
/// 多文件内容路由分类结果：按命中数分 routed(=1)/unmatched(0)/ambiguous(&gt;1)。
/// 仅做分类（只调 MatchFlowDefinitionsAsync），不触发导入。
/// </summary>
public record FileClassificationResult(
    IReadOnlyList<RoutedFile> Routed,
    IReadOnlyList<UnmatchedFile> Unmatched,
    IReadOnlyList<AmbiguousFile> Ambiguous);

public interface IBatchTriggerService
{
    Task<long> TriggerByFileUploadAsync(
        long flowDefinitionId,
        long orgId,
        long triggeredById,
        string filePath,
        Dictionary<string, string> columnMapping);

    Task ConfirmStagingAndFanOutAsync(long batchId);

    Task ProcessBatchJobAsync(BatchJob job, CancellationToken ct);

    /// <summary>
    /// 根据文件列头匹配流程定义（三轮策略：精确匹配 → 包含匹配 → 文件名回退），支持多流程同时命中
    /// </summary>
    /// <param name="fileColumns">文件列头名称集合</param>
    /// <param name="fileName">文件名（可选，用于文件名模式匹配）</param>
    /// <param name="orgId">组织ID</param>
    /// <returns>匹配到的 FlowDefinitionId + PluginRuleId 列表，空列表表示未匹配</returns>
    Task<List<MatchResult>> MatchFlowDefinitionsAsync(IReadOnlyList<string> fileColumns, string? fileName, long orgId);

    /// <summary>
    /// 多文件内容路由分类：对每个 (文件名, 列头) 调 MatchFlowDefinitionsAsync，
    /// 按命中数分 routed(=1)/unmatched(0)/ambiguous(&gt;1)。仅分类，不触发导入。
    /// </summary>
    Task<FileClassificationResult> ClassifyFilesAsync(IReadOnlyList<FileColumnHeader> files, long orgId);

    /// <summary>
    /// 获取可选流程定义列表（匹配失败时供前端选择）
    /// </summary>
    Task<List<FlowDefinitionCandidateDto>> GetFlowDefinitionCandidatesAsync(long orgId);
}

/// <summary>
/// CardFlow 批次触发服务：负责文件上传→解析→暂存→质检→fan-out 全链路（异步后台执行）
/// </summary>
public class BatchTriggerService : IBatchTriggerService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Channel<BatchJob> _channel;
    private readonly ILogger<BatchTriggerService> _logger;
    private const int MaxErrorMessageLength = 2000;

    /// <summary>
    /// 规则匹配候选项（从数据库动态构建）
    /// </summary>
    private record RuleMatchCandidate(
        long FlowDefinitionId,
        string FlowName,
        long PluginRuleId,
        string? FullColumnIdentifier,
        string? ColumnIdentifier
    );

    public BatchTriggerService(
        IServiceScopeFactory scopeFactory,
        Channel<BatchJob> channel,
        ILogger<BatchTriggerService> logger)
    {
        _scopeFactory = scopeFactory;
        _channel = channel;
        _logger = logger;
    }

    /// <summary>
    /// 文件上传触发：创建 CfBatch（FStatus=0 解析中）→ 投递解析任务 → 立即返回批次 ID
    /// </summary>
    public async Task<long> TriggerByFileUploadAsync(
        long flowDefinitionId,
        long orgId,
        long triggeredById,
        string filePath,
        Dictionary<string, string> columnMapping)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();

        var batch = new CfBatch
        {
            FFlowDefinitionId = flowDefinitionId,
            FOrgId = orgId,
            FTriggeredById = triggeredById,
            FTriggeredTime = DateTime.Now,
            FTriggerType = "fileUpload",
            FFilePath = filePath,
            FFileName = global::System.IO.Path.GetFileName(filePath),
            FColumnMappingJson = JsonSerializer.Serialize(columnMapping ?? new Dictionary<string, string>()),
            FStatus = 0,
            FCreatedTime = DateTime.Now,
        };

        db.Set<CfBatch>().Add(batch);
        await db.SaveChangesAsync();

        // 统一走批次级节点链处理
        await _channel.Writer.WriteAsync(new BatchJob(batch.FID, BatchJobKind.ProcessBatchStages));
        _logger.LogInformation("批次 {BatchId} 已创建并入队（流程 {FlowId} / 组织 {OrgId} / Kind=ProcessBatchStages）",
            batch.FID, flowDefinitionId, orgId);
        return batch.FID;
    }

    /// <summary>
    /// 用户在暂存查看后确认进入质检：FStatus 1→2 + 入队 fan-out 任务
    /// </summary>
    [Obsolete("已废弃：统一走 ProcessBatchStages 路径")]
    public async Task ConfirmStagingAndFanOutAsync(long batchId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();

        var batch = await db.Set<CfBatch>().FirstOrDefaultAsync(x => x.FID == batchId)
            ?? throw new InvalidOperationException($"批次 {batchId} 不存在");
        if (batch.FStatus != 1)
            throw new InvalidOperationException($"批次 {batchId} 当前状态={batch.FStatus}，仅 1=已暂存 状态可确认");

        batch.FStatus = 2; // 质检中
        batch.FUpdatedTime = DateTime.Now;
        await db.SaveChangesAsync();

        await _channel.Writer.WriteAsync(new BatchJob(batchId, BatchJobKind.QualityCheckAndFanOut));
        _logger.LogInformation("批次 {BatchId} 已确认暂存，已入队质检+fan-out", batchId);
    }

    /// <summary>
    /// 后台 Channel 消费入口：根据 JobKind 分发
    /// </summary>
    public async Task ProcessBatchJobAsync(BatchJob job, CancellationToken ct)
    {
        try
        {
            switch (job.Kind)
            {
                case BatchJobKind.ParseAndStage:
                case BatchJobKind.QualityCheckAndFanOut:
                case BatchJobKind.ProcessBatchStages:
                    await ProcessBatchStagesAsync(job.BatchId, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批次任务处理失败 BatchId={BatchId} Kind={Kind}", job.BatchId, job.Kind);
            await TryMarkBatchErrorAsync(job.BatchId, ex.Message);
        }
    }

    /// <summary>
    /// 新流程入口：调用 FlowEngineService.ProcessBatchStagesAsync，按 FSortOrder 顺序执行所有 batchAuto 节点
    /// </summary>
    private async Task ProcessBatchStagesAsync(long batchId, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
        var flowEngine = scope.ServiceProvider.GetRequiredService<IFlowEngineService>();

        var batch = await db.Set<CfBatch>().FirstOrDefaultAsync(x => x.FID == batchId, ct)
            ?? throw new InvalidOperationException($"批次 {batchId} 不存在");

        await flowEngine.ProcessBatchStagesAsync(batch, ct);
        _logger.LogInformation("批次 {BatchId} 节点链处理完成", batchId);
    }

    /// <summary>
    /// 解析 Excel → 写入 CfBatchRow → CfBatch.FStatus 0→1（已暂存）
    /// </summary>
    [Obsolete("已废弃：统一走 ProcessBatchStages 路径")]
    private async Task ParseAndStageAsync(long batchId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
        var parser = scope.ServiceProvider.GetRequiredService<IExcelParserService>();

        var batch = await db.Set<CfBatch>().FirstOrDefaultAsync(x => x.FID == batchId, ct)
            ?? throw new InvalidOperationException($"批次 {batchId} 不存在");

        if (batch.FStatus != 0)
        {
            _logger.LogWarning("批次 {BatchId} 状态={Status} 不是 0=解析中，跳过解析", batchId, batch.FStatus);
            return;
        }

        var mapping = string.IsNullOrEmpty(batch.FColumnMappingJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(batch.FColumnMappingJson)
                ?? new Dictionary<string, string>();

        var rows = await parser.ParseAsync(batch.FFilePath ?? string.Empty, mapping);

        // 幂等：清除已存在的明细（重试场景）
        var existing = db.Set<CfBatchRow>().Where(r => r.FBatchId == batchId);
        db.Set<CfBatchRow>().RemoveRange(existing);

        int rowNo = 0;
        foreach (var row in rows)
        {
            rowNo++;
            db.Set<CfBatchRow>().Add(new CfBatchRow
            {
                FBatchId = batchId,
                FRowNo = row.RowNo > 0 ? row.RowNo : rowNo,
                FDataJson = row.DataJson,
                FStatus = 0,
                FCreatedTime = DateTime.Now,
            });
        }

        batch.FTotalRows = rows.Count;
        batch.FStatus = 1; // 已暂存
        batch.FUpdatedTime = DateTime.Now;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("批次 {BatchId} 解析完成，共 {Count} 行，已进入暂存", batchId, rows.Count);
    }

    /// <summary>
    /// 质检 + 逐行 fan-out 创建卡片（每行独立事务 + 幂等）
    /// </summary>
    [Obsolete("已废弃：统一走 ProcessBatchStages 路径")]
    private async Task ExecuteQualityCheckAndFanOutAsync(long batchId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();

        var batch = await db.Set<CfBatch>().FirstOrDefaultAsync(x => x.FID == batchId, ct)
            ?? throw new InvalidOperationException($"批次 {batchId} 不存在");

        if (batch.FStatus != 2)
        {
            _logger.LogWarning("批次 {BatchId} 状态={Status} 不是 2=质检中，跳过 fan-out", batchId, batch.FStatus);
            return;
        }

        var currentVersion = await db.Set<CfFlowVersion>()
            .FirstOrDefaultAsync(v => v.FFlowDefinitionId == batch.FFlowDefinitionId && v.FIsCurrentVersion, ct)
            ?? throw new InvalidOperationException($"流程 {batch.FFlowDefinitionId} 没有可用版本");

        var rowIds = await db.Set<CfBatchRow>()
            .Where(r => r.FBatchId == batchId && (r.FStatus == 0 || r.FStatus == 2))
            .OrderBy(r => r.FRowNo)
            .Select(r => r.FID)
            .ToListAsync(ct);

        int success = 0, failed = 0;
        foreach (var rowId in rowIds)
        {
            ct.ThrowIfCancellationRequested();
            var (ok, err) = await ProcessSingleRowAsync(batchId, batch.FOrgId, batch.FFlowDefinitionId,
                currentVersion.FID, batch.FTriggeredById, rowId, ct);
            if (ok) success++; else failed++;
        }

        // 更新批次汇总
        await using (var summaryScope = _scopeFactory.CreateAsyncScope())
        {
            var sdb = summaryScope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
            var b = await sdb.Set<CfBatch>().FirstOrDefaultAsync(x => x.FID == batchId, ct);
            if (b != null)
            {
                b.FSuccessRows = success;
                b.FFailedRows = failed;
                b.FStatus = failed == 0 ? 3 : 3; // 进入"已创建卡片"，失败行保留待处理
                b.FUpdatedTime = DateTime.Now;
                await sdb.SaveChangesAsync(ct);
            }
        }

        _logger.LogInformation("批次 {BatchId} fan-out 完成：成功 {Success}, 失败 {Failed}", batchId, success, failed);
    }

    /// <summary>
    /// 单行处理（独立 scope/事务+幂等）：质检通过则创建卡片并回填 CfBatchRow.FCardId
    /// </summary>
    private async Task<(bool Ok, string? Err)> ProcessSingleRowAsync(
        long batchId, long orgId, long flowDefinitionId, long flowVersionId,
        long triggeredById, long rowId, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();

            var row = await db.Set<CfBatchRow>().FirstOrDefaultAsync(r => r.FID == rowId, ct);
            if (row == null) return (false, "行不存在");

            // 幂等：已成功创建卡片的行直接跳过
            if (row.FStatus == 3 && row.FCardId.HasValue) return (true, null);

            // 质检（占位：后续 Task 4 接入规则引擎）
            var qcError = ValidateRow(row);
            if (qcError != null)
            {
                row.FStatus = 2; // 质检失败
                row.FErrorMessage = qcError;
                row.FUpdatedTime = DateTime.Now;
                await db.SaveChangesAsync(ct);
                return (false, qcError);
            }

            // 创建卡片
            var card = new CfCard
            {
                FFlowDefinitionId = flowDefinitionId,
                FFlowVersionId = flowVersionId,
                FStatus = "draft",
                FInitiatorId = triggeredById,
                FInitiatorName = string.Empty,
                FDataJson = row.FDataJson,
                FOrgId = orgId,
                FCreatedTime = DateTime.Now,
                FCurrentRound = 1,
                FBatchId = batchId,
            };
            db.Set<CfCard>().Add(card);
            await db.SaveChangesAsync(ct);

            row.FStatus = 3; // 已创建卡片
            row.FCardId = card.FID;
            row.FErrorMessage = null;
            row.FUpdatedTime = DateTime.Now;
            await db.SaveChangesAsync(ct);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批次 {BatchId} 行 {RowId} 处理失败", batchId, rowId);
            await TryMarkRowErrorAsync(rowId, ex.Message);
            return (false, ex.Message);
        }
    }

    private static string? ValidateRow(CfBatchRow row)
    {
        if (string.IsNullOrWhiteSpace(row.FDataJson)) return "数据为空";
        return null;
    }

    private async Task TryMarkBatchErrorAsync(long batchId, string error)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
            var b = await db.Set<CfBatch>().FirstOrDefaultAsync(x => x.FID == batchId);
            if (b != null)
            {
                b.FErrorMessage = Truncate(error, MaxErrorMessageLength);
                b.FUpdatedTime = DateTime.Now;
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex2)
        {
            _logger.LogError(ex2, "标记批次 {BatchId} 错误失败", batchId);
        }
    }

    private async Task TryMarkRowErrorAsync(long rowId, string error)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
            var r = await db.Set<CfBatchRow>().FirstOrDefaultAsync(x => x.FID == rowId);
            if (r != null)
            {
                r.FStatus = 2;
                r.FErrorMessage = Truncate(error, MaxErrorMessageLength);
                r.FUpdatedTime = DateTime.Now;
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex2)
        {
            _logger.LogError(ex2, "标记批次行 {RowId} 错误失败", rowId);
        }
    }

    private static string Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty : (s!.Length > max ? s[..max] : s);

    // ═══════════════════════════════════════════════════════════
    // 文件匹配逻辑（从 PipelineMatchingService 迁移）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 从数据库动态读取所有已发布流程的首节点 ExcelInput 插件规则配置，构建匹配候选项
    /// </summary>
    private async Task<List<RuleMatchCandidate>> BuildRuleCandidatesAsync(STOTOPDbContext db, long orgId)
    {
        // 四表 JOIN: CfFlowDefinition -> CfFlowVersion -> CfStageDefinition -> CfPluginRule
        // 条件: FOrgId == orgId, FStatus == "published", FIsCurrentVersion, MIN(FSortOrder)首节点, F插件规则ID IS NOT NULL, F类型编码 == "excelInput"
        var rawCandidates = await (
            from fd in db.Set<CfFlowDefinition>()
            join fv in db.Set<CfFlowVersion>() on fd.FID equals fv.FFlowDefinitionId
            join sd in db.Set<CfStageDefinition>() on fv.FID equals sd.FFlowVersionId
            join pr in db.Set<CfPluginRule>() on sd.F插件规则ID equals pr.FID
            where fd.FOrgId == orgId
                  && fd.FStatus == "published"
                  && fv.FIsCurrentVersion
                  && sd.F插件规则ID != null
                  && pr.F类型编码 == "excelInput"
            group new { sd, pr } by new { fd.FID, fd.FFlowName } into g
            select new
            {
                FlowDefinitionId = g.Key.FID,
                FlowName = g.Key.FFlowName,
                // 取 MIN(FSortOrder) 的首节点
                FirstStage = g.OrderBy(x => x.sd.FSortOrder).First()
            }
        ).ToListAsync();

        var candidates = new List<RuleMatchCandidate>();
        foreach (var rc in rawCandidates)
        {
            var pluginRule = rc.FirstStage.pr;
            string? fullColumnId = null;
            string? columnId = null;

            if (!string.IsNullOrEmpty(pluginRule.F规则配置JSON))
            {
                try
                {
                    using var doc = JsonDocument.Parse(pluginRule.F规则配置JSON);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("fullColumnIdentifier", out var fciEl))
                        fullColumnId = fciEl.GetString();
                    if (root.TryGetProperty("columnIdentifier", out var ciEl))
                        columnId = ciEl.GetString();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex,
                        "解析插件规则 F规则配置JSON 失败: PluginRuleId={RuleId}, FlowDefinitionId={FlowId}",
                        pluginRule.FID, rc.FlowDefinitionId);
                }
            }

            candidates.Add(new RuleMatchCandidate(
                rc.FlowDefinitionId,
                rc.FlowName,
                pluginRule.FID,
                fullColumnId,
                columnId
            ));
        }

        _logger.LogDebug("构建匹配候选项完成: 组织={OrgId}, 候选数={Count}", orgId, candidates.Count);
        return candidates;
    }

    /// <summary>
    /// 根据文件列头匹配流程定义（三轮策略：精确匹配 → 包含匹配 → 文件名回退），支持多流程同时命中
    /// </summary>
    /// <param name="fileColumns">文件列头名称集合</param>
    /// <param name="fileName">文件名（可选，用于文件名模式匹配）</param>
    /// <param name="orgId">组织ID</param>
    /// <returns>匹配到的 FlowDefinitionId + PluginRuleId 列表，空列表表示未匹配</returns>
    public async Task<List<MatchResult>> MatchFlowDefinitionsAsync(IReadOnlyList<string> fileColumns, string? fileName, long orgId)
    {
        if (fileColumns == null || fileColumns.Count == 0)
        {
            _logger.LogWarning("文件列头为空，无法匹配流程定义");
            return [];
        }

        var columnSet = new HashSet<string>(fileColumns, StringComparer.OrdinalIgnoreCase);
        // 排序后拼接，确保与 fullColumnIdentifier 比对时顺序一致
        var sortedColumnString = string.Join(",", fileColumns
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase));

        _logger.LogDebug("开始匹配流程定义，组织={OrgId}，文件列名数={Count}", orgId, fileColumns.Count);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();

        // 从插件规则构建候选项
        var candidates = await BuildRuleCandidatesAsync(db, orgId);

        // ═══ 第一轮：fullColumnIdentifier 精确匹配 ═══
        var round1Hits = candidates
            .Where(c => !string.IsNullOrEmpty(c.FullColumnIdentifier))
            .Where(c =>
            {
                var expected = SortColumnIdentifier(c.FullColumnIdentifier!);
                return string.Equals(sortedColumnString, expected, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        if (round1Hits.Count > 0)
        {
            _logger.LogInformation(
                "第一轮精确匹配命中(fullColumnIdentifier): 命中数={Count}, 流程=[{Flows}]",
                round1Hits.Count,
                string.Join(", ", round1Hits.Select(h => $"{h.FlowName}({h.FlowDefinitionId})")));
            return round1Hits.Select(h => new MatchResult(h.FlowDefinitionId, h.PluginRuleId)).ToList();
        }

        // ═══ 第二轮：columnIdentifier 包含匹配 ═══
        var round2Hits = candidates
            .Where(c => !string.IsNullOrEmpty(c.ColumnIdentifier))
            .Where(c =>
            {
                var requiredColumns = c.ColumnIdentifier!
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return requiredColumns.All(col => columnSet.Contains(col));
            })
            .ToList();

        if (round2Hits.Count > 0)
        {
            _logger.LogInformation(
                "第二轮包含匹配命中(columnIdentifier): 命中数={Count}, 流程=[{Flows}]",
                round2Hits.Count,
                string.Join(", ", round2Hits.Select(h => $"{h.FlowName}({h.FlowDefinitionId})")));
            return round2Hits.Select(h => new MatchResult(h.FlowDefinitionId, h.PluginRuleId)).ToList();
        }

        // ═══ 第三轮（回退）：fileNamePattern 文件名模式匹配 ═══
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            // 从 CfFlowDefinition.FMatchPattern 读取 fileNamePattern（保留回退能力）
            var flowDefinitions = await db.Set<CfFlowDefinition>()
                .Where(fd => fd.FOrgId == orgId
                             && fd.FStatus == "published"
                             && fd.FMatchPattern != null)
                .ToListAsync();

            var round3Results = new List<MatchResult>();
            foreach (var fd in flowDefinitions)
            {
                var matchPattern = ParseMatchPattern(fd.FMatchPattern!);
                if (matchPattern == null) continue;

                if (!string.IsNullOrEmpty(matchPattern.FileNamePattern))
                {
                    var regexPattern = "^" + Regex.Escape(matchPattern.FileNamePattern)
                        .Replace("\\*", ".*") + "$";
                    if (Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase))
                    {
                        // 需要查找该流程首节点的 PluginRuleId
                        var firstStageRuleId = await (
                            from fv in db.Set<CfFlowVersion>()
                            join sd in db.Set<CfStageDefinition>() on fv.FID equals sd.FFlowVersionId
                            where fv.FFlowDefinitionId == fd.FID
                                  && fv.FIsCurrentVersion
                                  && sd.F插件规则ID != null
                            orderby sd.FSortOrder
                            select sd.F插件规则ID!.Value
                        ).FirstOrDefaultAsync();

                        _logger.LogInformation(
                            "第三轮文件名回退命中(fileNamePattern): FlowDefinitionId={FlowId}, FlowName={FlowName}, Pattern={Pattern}, PluginRuleId={RuleId}",
                            fd.FID, fd.FFlowName, matchPattern.FileNamePattern, firstStageRuleId);

                        round3Results.Add(new MatchResult(fd.FID, firstStageRuleId));
                    }
                }
            }

            if (round3Results.Count > 0)
                return round3Results;
        }

        _logger.LogDebug("未找到匹配的流程定义，组织={OrgId}", orgId);
        return [];
    }

    /// <summary>
    /// 多文件内容路由分类：对每个 (文件名, 列头) 调 MatchFlowDefinitionsAsync，
    /// 按命中数分 routed(=1)/unmatched(0)/ambiguous(&gt;1)。仅分类，不触发导入。
    /// </summary>
    public async Task<FileClassificationResult> ClassifyFilesAsync(IReadOnlyList<FileColumnHeader> files, long orgId)
    {
        var routed = new List<RoutedFile>();
        var unmatched = new List<UnmatchedFile>();
        var ambiguous = new List<AmbiguousFile>();

        foreach (var f in files ?? [])
        {
            var matches = await MatchFlowDefinitionsAsync(f.Columns, f.FileName, orgId);
            switch (matches.Count)
            {
                case 0:
                    unmatched.Add(new UnmatchedFile(f.FileName, f.Columns));
                    break;
                case 1:
                    routed.Add(new RoutedFile(f.FileName, matches[0].FlowDefinitionId, matches[0].PluginRuleId));
                    break;
                default:
                    ambiguous.Add(new AmbiguousFile(f.FileName, matches));
                    break;
            }
        }

        _logger.LogInformation(
            "多文件路由分类完成：组织={OrgId}，输入={Total}，routed={Routed}，unmatched={Unmatched}，ambiguous={Ambiguous}",
            orgId, files?.Count ?? 0, routed.Count, unmatched.Count, ambiguous.Count);

        return new FileClassificationResult(routed, unmatched, ambiguous);
    }

    /// <summary>
    /// 获取可选流程定义列表（匹配失败时供前端选择）
    /// </summary>
    public async Task<List<FlowDefinitionCandidateDto>> GetFlowDefinitionCandidatesAsync(long orgId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();

        return await db.Set<CfFlowDefinition>()
            .Where(fd => fd.FOrgId == orgId && fd.FStatus == "published")
            .OrderBy(fd => fd.FFlowName)
            .Select(fd => new FlowDefinitionCandidateDto
            {
                FID = fd.FID,
                FFlowName = fd.FFlowName,
                FFlowCode = fd.FFlowCode,
                FDescription = fd.FDescription,
            })
            .ToListAsync();
    }

    // F匹配规则 JSON 的键为 camelCase（fileNamePattern/columnIdentifier/fullColumnIdentifier），
    // 而 MatchPatternData 属性为 PascalCase；默认反序列化区分大小写会全部映射为 null，
    // 导致第三轮 fileNamePattern 回退完全失效（投诉账单/小件员等退化表头源无法路由）。
    private static readonly JsonSerializerOptions MatchPatternJsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 解析 FMatchPattern JSON 为 MatchPatternData 对象
    /// </summary>
    private static MatchPatternData? ParseMatchPattern(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<MatchPatternData>(json, MatchPatternJsonOpts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 将逗号分隔的列标识符排序后拼接，确保与文件列比对时顺序一致
    /// </summary>
    private static string SortColumnIdentifier(string identifier)
    {
        var parts = identifier.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(",", parts.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// FMatchPattern JSON 反序列化模型
    /// </summary>
    private class MatchPatternData
    {
        public string? FullColumnIdentifier { get; set; }
        public string? ColumnIdentifier { get; set; }
        public string? FileNamePattern { get; set; }
    }
}
