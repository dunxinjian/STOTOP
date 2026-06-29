using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.CardFlow.Services.Handlers;

public class VoucherGenerationService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly AutoVoucherHandler _handler;
    private readonly ILogger<VoucherGenerationService> _logger;
    private readonly SystemServiceAccountOptions _serviceAccountOptions;

    public VoucherGenerationService(
        STOTOPDbContext dbContext,
        AutoVoucherHandler handler,
        ILogger<VoucherGenerationService> logger,
        IOptions<SystemServiceAccountOptions> serviceAccountOptions)
    {
        _dbContext = dbContext;
        _handler = handler;
        _logger = logger;
        _serviceAccountOptions = serviceAccountOptions.Value;
    }

    /// <summary>查询凭证生成记录列表</summary>
    public async Task<List<VoucherGenerationRecordDto>> GetRecordsAsync(long? batchId = null, long? fileTypeId = null)
    {
        var query = _dbContext.Set<CfVoucherRecord>().AsNoTracking().AsQueryable();
        if (batchId.HasValue) query = query.Where(r => r.FBatchId == batchId.Value);

        var records = await query.OrderByDescending(r => r.FCreatedTime).ToListAsync();

        return records.Select(MapToDto).ToList();
    }

    /// <summary>查询单条记录详情</summary>
    public async Task<VoucherGenerationRecordDto?> GetRecordAsync(long id)
    {
        var record = await _dbContext.Set<CfVoucherRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        return record == null ? null : MapToDto(record);
    }

    /// <summary>重试生成凭证（仅处理未匹配的行）</summary>
    public async Task<HandlerResult> RetryAsync(long recordId)
    {
        // 1. 查询原记录
        var record = await _dbContext.Set<CfVoucherRecord>().FindAsync(recordId);
        if (record == null)
            return HandlerResult.Fail("记录不存在");

        if (record.FStatus != 2 && record.FStatus != 3)
            return HandlerResult.Fail("只有部分成功或全部失败的记录才能重试");

        // 2. 解析未匹配明细，获取行 ID 列表
        var unmatchedDetails = DeserializeUnmatchedDetails(record.FUnmatchedDetailsJson);
        var unmatchedRowIds = unmatchedDetails?.Select(d => d.RowId).Distinct().ToList();
        if (unmatchedRowIds == null || unmatchedRowIds.Count == 0)
            return HandlerResult.Fail("没有未匹配的行");

        // 3. 查询关联的 AutoVoucher 派发规则配置
        var rule = await _dbContext.Set<CfDispatchRule>()
            .AsNoTracking()
            .Where(r => r.FHandlerType == "AutoVoucher" && r.FStatus == 1)
            .OrderBy(r => r.FPriority)
            .FirstOrDefaultAsync();

        string? handlerConfig = rule?.FHandlerConfigJson;

        // 如果路由配置中没有 FileTypeId，DC文件类型已废除，不再注入
        if (!string.IsNullOrEmpty(handlerConfig))
        {
            // 保持原始配置
        }
        else
        {
            // 没有路由配置，构建默认配置
            handlerConfig = "{\"mode\":\"rulesBased\"}";
        }

        // 4. 从规则配置中读取日期字段名（默认 F业务日期）
        string dateField = "F业务日期";
        if (!string.IsNullOrEmpty(rule?.FHandlerConfigJson))
        {
            try
            {
                using var configDoc = JsonDocument.Parse(rule.FHandlerConfigJson);
                if (configDoc.RootElement.TryGetProperty("dateField", out var df) && df.ValueKind == JsonValueKind.String)
                    dateField = df.GetString() ?? "F业务日期";
            }
            catch { }
        }

        // 5. 优先从暂存表获取业务日期来确定会计期间
        var businessDate = await GetBusinessDateFromStagingTableAsync(record.FBatchId, record.FTargetTable, dateField);
        var queryDate = businessDate ?? DateTime.Now;

        long? periodId = null;
        long? accountSetId = null;
        try
        {
            var period = await _dbContext.Set<FinAccountPeriod>()
                .Where(p => p.FStartDate <= queryDate && p.FEndDate >= queryDate && p.FIsClosed == 0)
                .OrderByDescending(p => p.FStartDate)
                .FirstOrDefaultAsync();

            // 期间不存在则自动创建（双检查 + 捕获冲突重查）
            if (period == null)
            {
                // 查默认账套
                var defaultAccountSet = await _dbContext.Set<FinAccountSet>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.FIsDefault && a.FStatus == 1);
                long targetAccountSetId = defaultAccountSet?.FID ?? 0;
                long targetOrgId = defaultAccountSet?.FOrgId ?? 0;

                if (targetAccountSetId > 0)
                {
                    var startDate = new DateTime(queryDate.Year, queryDate.Month, 1);
                    var endDate = startDate.AddMonths(1).AddDays(-1);
                    try
                    {
                        period = new FinAccountPeriod
                        {
                            FYear = queryDate.Year,
                            FPeriodNo = queryDate.Month,
                            FStartDate = startDate,
                            FEndDate = endDate,
                            FIsClosed = 0,
                            FStatus = 1,
                            FAccountSetId = targetAccountSetId,
                            FOrgId = targetOrgId,
                            FCreatedTime = DateTime.Now,
                            FUpdatedTime = DateTime.Now
                        };
                        _dbContext.Set<FinAccountPeriod>().Add(period);
                        await _dbContext.SaveChangesAsync();
                        _logger.LogInformation("重试: 自动创建会计期间: {Year}年第{Month}期, 账套={AccountSetId}",
                            period.FYear, period.FPeriodNo, period.FAccountSetId);
                    }
                    catch (DbUpdateException ex)
                    {
                        // 唯一约束冲突 → 其他线程已创建成功，分离失败实体并重新查询
                        _logger.LogInformation(ex, "重试: 会计期间并发创建冲突，重新查询已有记录");
                        if (period != null) _dbContext.Entry(period).State = EntityState.Detached;
                        period = await _dbContext.Set<FinAccountPeriod>()
                            .Where(p => p.FStartDate <= queryDate && p.FEndDate >= queryDate && p.FIsClosed == 0)
                            .OrderByDescending(p => p.FStartDate)
                            .FirstOrDefaultAsync();
                    }
                }
            }

            if (period != null)
            {
                periodId = period.FID;
                accountSetId = period.FAccountSetId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "重试: 获取会计期间失败（查询日期: {QueryDate}）", queryDate);
        }

        if (!periodId.HasValue)
            return HandlerResult.Fail("当前没有活跃的会计期间，无法生成凭证");

        // 6. 构建 HandlerContext，限定只处理未匹配的行
        var context = new HandlerContext
        {
            BatchId = record.FBatchId,
            TargetTable = record.FTargetTable,
            Classification = new ClassificationItem
            {
                Type = "VoucherRetry",
                Severity = "Info",
                AffectedRowIds = unmatchedRowIds,
                AffectedRowCount = unmatchedRowIds.Count
            },
            HandlerConfig = handlerConfig,
            OrgId = _serviceAccountOptions.OrgId,
            CreatorId = _serviceAccountOptions.UserId,
            PeriodId = periodId,
            AccountSetId = accountSetId,
            BusinessDate = businessDate
        };

        // 7. 调用 AutoVoucherHandler 处理
        HandlerResult result;
        try
        {
            result = await _handler.HandleAsync(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重试凭证生成异常: RecordId={RecordId}", recordId);
            record.FErrorMessage = $"重试异常: {ex.Message}";
            record.FUpdatedTime = DateTime.Now;
            await _dbContext.SaveChangesAsync();
            return HandlerResult.Fail($"重试异常: {ex.Message}");
        }

        // 8. 合并结果：更新原记录
        if (result.Success)
        {
            // 从 handler 的 output 获取统计信息
            int newMatchedRows = 0;
            int newUnmatchedRows = 0;
            if (result.Output.TryGetValue("MatchedRows", out var mr)) newMatchedRows = Convert.ToInt32(mr);
            if (result.Output.TryGetValue("UnmatchedRows", out var ur)) newUnmatchedRows = Convert.ToInt32(ur);

            record.FMatchedRows += newMatchedRows;
            record.FUnmatchedRows = newUnmatchedRows;
            record.FStatus = newUnmatchedRows == 0 ? 1 : 2;

            // 合并新的凭证 ID
            if (result.Output.TryGetValue("GeneratedVoucherIds", out var vIds) && vIds is List<long> newVoucherIds)
            {
                var existingIds = DeserializeVoucherIds(record.FVoucherIdsJson);
                existingIds.AddRange(newVoucherIds);
                record.FVoucherIdsJson = JsonSerializer.Serialize(existingIds);
                record.FGeneratedVoucherCount = existingIds.Count;
            }

            // 注意：新的 HandleRulesBasedAsync 会创建新的 record，
            // 这里我们更新旧记录（新 record 仅包含重试部分的信息）
            record.FErrorMessage = null;
            record.FUpdatedTime = DateTime.Now;

            _logger.LogInformation("重试成功: RecordId={RecordId}, 新匹配{NewMatched}行, 剩余未匹配{Remaining}行",
                recordId, newMatchedRows, newUnmatchedRows);
        }
        else
        {
            record.FErrorMessage = result.Message;
            record.FUpdatedTime = DateTime.Now;
        }

        _dbContext.Set<CfVoucherRecord>().Update(record);
        await _dbContext.SaveChangesAsync();

        return result;
    }

    /// <summary>从暂存表查询指定日期字段</summary>
    private async Task<DateTime?> GetBusinessDateFromStagingTableAsync(long batchId, string targetTable, string dateField)
    {
        try
        {
            var sql = $"SELECT TOP 1 [{dateField}] FROM [{targetTable}] WHERE [F批次ID] = @BatchId AND [{dateField}] IS NOT NULL ORDER BY [{dateField}]";
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();
            var businessDate = await SqlMapper.ExecuteScalarAsync<DateTime?>(connection, sql, new { BatchId = batchId });
            return businessDate;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "重试: 无法从暂存表 {Table} 获取日期字段 {DateField}，将使用当前时间", targetTable, dateField);
            return null;
        }
    }

    private static VoucherGenerationRecordDto MapToDto(CfVoucherRecord r)
    {
        return new VoucherGenerationRecordDto
        {
            Id = r.FID,
            BatchId = r.FBatchId,
            TargetTable = r.FTargetTable,
            TotalRows = r.FTotalRows,
            MatchedRows = r.FMatchedRows,
            UnmatchedRows = r.FUnmatchedRows,
            UnmatchedDetails = DeserializeUnmatchedDetails(r.FUnmatchedDetailsJson)?
                .Select(d => new UnmatchedDetailDto
                {
                    RowId = d.RowId,
                    FieldName = d.FieldName,
                    FieldValue = d.FieldValue,
                    Reason = d.Reason
                }).ToList(),
            GeneratedVoucherCount = r.FGeneratedVoucherCount,
            VoucherIds = DeserializeVoucherIds(r.FVoucherIdsJson),
            Status = r.FStatus,
            ErrorMessage = r.FErrorMessage,
            CreateTime = r.FCreatedTime,
            UpdateTime = r.FUpdatedTime
        };
    }

    private static List<UnmatchedDetailItem>? DeserializeUnmatchedDetails(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<UnmatchedDetailItem>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private static List<long> DeserializeVoucherIds(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<long>();
        try
        {
            return JsonSerializer.Deserialize<List<long>>(json) ?? new List<long>();
        }
        catch
        {
            return new List<long>();
        }
    }

    /// <summary>未匹配明细的反序列化模型</summary>
    private class UnmatchedDetailItem
    {
        public long RowId { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string FieldValue { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
