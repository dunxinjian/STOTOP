using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.Finance.Constants;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;

namespace STOTOP.Module.CardFlow.AutoPlugin.Implementations;

/// <summary>外部凭证迁移插件，将STG暂存表中的外部财务凭证数据映射并生成为本系统FIN凭证+分录</summary>
public partial class VoucherMigrationPlugin : BatchPluginBase
{
    private readonly STOTOPDbContext _dbContext;
    private readonly MigrationMappingService _mappingService;
    private readonly IPluginProgressReporter _progressReporter;
    private readonly ILogger<VoucherMigrationPlugin> _logger;

    public override string PluginName => "VoucherMigration";
    public override string DisplayName => "凭证迁移";

    // 合法表名：以 STG 开头
    [GeneratedRegex(@"^STG[\u4e00-\u9fa5A-Za-z0-9_]+$")]
    private static partial Regex ValidTableNameRegex();

    public VoucherMigrationPlugin(
        STOTOPDbContext dbContext,
        MigrationMappingService mappingService,
        IPluginProgressReporter progressReporter,
        ILogger<VoucherMigrationPlugin> logger)
    {
        _dbContext = dbContext;
        _mappingService = mappingService;
        _progressReporter = progressReporter;
        _logger = logger;
    }

    public override async Task<PluginResult> ExecuteAsync(PluginContext context)
    {
        const int totalSteps = 4;

        // Step 1: 解析配置
        await _progressReporter.ReportProgressAsync(context.BatchId, 0, totalSteps, "解析配置");

        VoucherMigrationConfig config;
        try
        {
            config = await ParseConfigAsync(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VoucherMigrationPlugin: 配置解析失败, BatchId={BatchId}", context.BatchId);
            return PluginResult.Fail($"配置解析失败: {ex.Message}");
        }

        if (config.SchemeId == Guid.Empty)
            return PluginResult.Fail("F配置JSON中未指定schemeId（迁移方案ID）");

        await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "解析配置");

        // Step 2: 加载迁移方案和映射
        await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "加载映射");

        var scheme = await _dbContext.Set<FinMigrationScheme>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.FID == config.SchemeId);
        if (scheme == null)
            return PluginResult.Fail($"迁移方案不存在: {config.SchemeId}");

        var accountMappings = await _mappingService.LoadAccountMappingsAsync(config.SchemeId);
        var auxMappings = await _mappingService.LoadAuxMappingsAsync(config.SchemeId);
        var assetMappings = await _mappingService.LoadAssetMappingsAsync(config.SchemeId);

        _logger.LogInformation("VoucherMigrationPlugin: 加载映射完成 - 科目映射={AccountCount}, 辅助映射={AuxCount}, 资产映射={AssetCount}",
            accountMappings.Count, auxMappings.Count, assetMappings.Count);

        await _progressReporter.ReportProgressAsync(context.BatchId, 2, totalSteps, "加载映射");

        // Step 3: 读取暂存表数据并执行映射
        await _progressReporter.ReportProgressAsync(context.BatchId, 2, totalSteps, "数据映射");

        var targetTable = GetTargetTable(context);

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        var queryParams = new DynamicParameters();
        queryParams.Add("BatchId", context.BatchId);
        var sql = $"SELECT * FROM [{targetTable}] WHERE [F批次ID] = @BatchId ORDER BY [F原始行号]";

        var rows = (await connection.QueryAsync(sql, queryParams))
            .Select(r => (IDictionary<string, object>)r)
            .ToList();

        if (rows.Count == 0)
            return PluginResult.Fail("暂存表中无匹配数据");

        _logger.LogInformation("VoucherMigrationPlugin: 读取暂存表数据 {Count} 行", rows.Count);

        // 执行映射转换
        var missingAccounts = new Dictionary<string, int>();
        var missingAux = new Dictionary<string, int>();
        var missingAssets = new Dictionary<string, int>();
        var voucherGroups = new Dictionary<string, VoucherGroupData>();
        int skippedRows = 0;

        foreach (var row in rows)
        {
            var sourceCode = GetFieldValue(row, config.AccountCodeField);
            if (string.IsNullOrWhiteSpace(sourceCode))
            {
                skippedRows++;
                continue;
            }

            var summaryValue = GetFieldValue(row, config.SummaryField);
            var debitAmount = GetDecimalValue(row, config.DebitField);
            var creditAmount = GetDecimalValue(row, config.CreditField);
            var direction = debitAmount > 0 ? "debit" : "credit";
            var amount = debitAmount > 0 ? debitAmount : creditAmount;

            var accountResult = _mappingService.ResolveAccount(sourceCode, accountMappings, summaryValue, direction, amount);
            if (accountResult == null)
            {
                missingAccounts[sourceCode] = missingAccounts.GetValueOrDefault(sourceCode) + 1;
                skippedRows++;
                continue;
            }

            string? auxJson = null;
            if (!string.IsNullOrEmpty(config.AuxiliaryField))
            {
                auxJson = ResolveAuxiliaryFromRow(row, config, auxMappings, scheme.F辅助项缺失策略, missingAux);
            }

            long? assetCardId = null;
            if (!string.IsNullOrEmpty(config.AssetCodeField))
            {
                var assetCode = GetFieldValue(row, config.AssetCodeField);
                if (!string.IsNullOrEmpty(assetCode))
                {
                    assetCardId = _mappingService.ResolveAsset(assetCode, assetMappings);
                    if (assetCardId == null)
                        missingAssets[assetCode] = missingAssets.GetValueOrDefault(assetCode) + 1;
                }
            }

            var voucherDate = GetFieldValue(row, config.VoucherDateField) ?? "";
            var voucherNo = GetFieldValue(row, config.VoucherNoField) ?? "";
            var voucherWord = GetFieldValue(row, config.VoucherWordField) ?? "记";
            var groupKey = $"{voucherDate}_{voucherNo}";

            if (!voucherGroups.ContainsKey(groupKey))
            {
                voucherGroups[groupKey] = new VoucherGroupData
                {
                    VoucherDate = ParseDate(voucherDate),
                    VoucherWord = voucherWord,
                    Entries = new List<EntryData>()
                };
            }

            voucherGroups[groupKey].Entries.Add(new EntryData
            {
                TargetAccountId = accountResult.Value.targetId ?? 0,
                TargetAccountCode = accountResult.Value.targetCode,
                TargetAccountName = accountResult.Value.targetName,
                AuxJson = auxJson,
                DebitAmount = debitAmount,
                CreditAmount = creditAmount,
                Summary = summaryValue ?? "",
                AssetCardId = assetCardId
            });
        }

        await _progressReporter.ReportProgressAsync(context.BatchId, 3, totalSteps, "数据映射");

        // Step 4: 生成凭证
        await _progressReporter.ReportProgressAsync(context.BatchId, 3, totalSteps, "生成凭证");

        if (voucherGroups.Count == 0)
        {
            var report = BuildMissingReport(missingAccounts, missingAux, missingAssets, 0, skippedRows, rows.Count);
            return PluginResult.Fail($"无有效凭证数据可生成。{report}");
        }

        var voucherOrgId = await GetOrgIdAsync(context);
        if (voucherOrgId <= 0) voucherOrgId = scheme.F组织ID;
        var voucherAccountSetId = await GetAccountSetIdAsync(context) ?? scheme.F目标账套ID;

        int voucherCount = 0;
        int entryCount = 0;
        var now = DateTime.Now;

        foreach (var (groupKey, group) in voucherGroups)
        {
            long periodId = 0;
            if (voucherAccountSetId > 0 && group.VoucherDate.HasValue)
            {
                var period = await _dbContext.Set<FinAccountPeriod>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.FStartDate <= group.VoucherDate.Value
                                           && p.FEndDate >= group.VoucherDate.Value
                                           && p.FAccountSetId == voucherAccountSetId
                                           && p.FStatus == 1);
                if (period != null)
                    periodId = period.FID;
            }

            var voucher = new FinVoucher
            {
                FVoucherWord = group.VoucherWord,
                FVoucherNo = 0,
                FDate = group.VoucherDate ?? now,
                FPeriodId = periodId,
                FAccountSetId = voucherAccountSetId,
                FOrgId = voucherOrgId,
                FSource = "数据迁移",
                FDataScopeId = context.BatchId.ToString(),
                FStatus = 0,
                FCreator = "数据迁移系统",
                FCreatedTime = now,
                FUpdatedTime = now,
                Entries = group.Entries.Select((e, i) => new FinVoucherEntry
                {
                    FLineNo = i + 1,
                    FAccountId = e.TargetAccountId,
                    FAccountCode = e.TargetAccountCode,
                    FAccountName = e.TargetAccountName,
                    FAuxiliaryJson = e.AuxJson,
                    FDebitAmount = e.DebitAmount,
                    FCreditAmount = e.CreditAmount,
                    FSummary = e.Summary,
                    FDataScopeId = context.BatchId.ToString(),
                    FOrgId = voucherOrgId,
                    FCreatedTime = now,
                    FUpdatedTime = now
                }).ToList()
            };

            _dbContext.Set<FinVoucher>().Add(voucher);
            voucherCount++;
            entryCount += voucher.Entries.Count;
        }

        await _dbContext.SaveChangesAsync();

        await _progressReporter.ReportProgressAsync(context.BatchId, 4, totalSteps, "生成凭证");

        var hasFailures = config.FailOnMissing && missingAccounts.Any();
        var message = BuildMissingReport(missingAccounts, missingAux, missingAssets, voucherCount, skippedRows, rows.Count);

        _logger.LogInformation("VoucherMigrationPlugin: 完成 - 生成凭证={VoucherCount}, 分录={EntryCount}, 缺失科目={MissingCount}",
            voucherCount, entryCount, missingAccounts.Count);

        var result = hasFailures
            ? PluginResult.Fail(message)
            : PluginResult.Ok(message, voucherCount);
        result.FailedRows = skippedRows;
        return result;
    }

    public override async Task RollbackAsync(PluginContext context)
    {
        var batchIdStr = context.BatchId.ToString();
        var vouchers = await _dbContext.Set<FinVoucher>()
            .Where(v => v.FDataScopeId == batchIdStr && v.FSource == "数据迁移")
            .Include(v => v.Entries)
            .ToListAsync();

        if (vouchers.Any())
        {
            foreach (var v in vouchers)
                _dbContext.Set<FinVoucherEntry>().RemoveRange(v.Entries);
            _dbContext.Set<FinVoucher>().RemoveRange(vouchers);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("VoucherMigrationPlugin: 回撤完成 - 删除凭证={Count}, BatchId={BatchId}",
                vouchers.Count, context.BatchId);
        }
    }

    public override PluginMetadata GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata.Description = "将外部财务系统的凭证数据通过映射配置转换为本系统FIN凭证+分录";
        return metadata;
    }

    #region 私有方法

    private string GetTargetTable(PluginContext context)
    {
        var batch = _dbContext.Set<CfBatch>()
            .AsNoTracking()
            .FirstOrDefault(b => b.FID == context.BatchId);
        var tableName = batch?.FActualTargetTable;

        if (string.IsNullOrWhiteSpace(tableName))
            throw new InvalidOperationException("批次记录中未找到暂存表信息（FActualTargetTable为空）");

        if (!ValidTableNameRegex().IsMatch(tableName))
            throw new InvalidOperationException($"非法的暂存表名: {tableName}");

        return tableName;
    }

    private async Task<long> GetOrgIdAsync(PluginContext context)
    {
        var batch = await _dbContext.Set<CfBatch>()
            .AsNoTracking()
            .Where(b => b.FID == context.BatchId)
            .Select(b => b.FOrgId)
            .FirstOrDefaultAsync();
        return batch;
    }

    private async Task<long?> GetAccountSetIdAsync(PluginContext context)
    {
        var batch = await _dbContext.Set<CfBatch>()
            .AsNoTracking()
            .Where(b => b.FID == context.BatchId)
            .Select(b => b.FAccountSetId)
            .FirstOrDefaultAsync();
        return batch;
    }

    private async Task<VoucherMigrationConfig> ParseConfigAsync(PluginContext context)
    {
        var config = new VoucherMigrationConfig();

        if (!context.PluginRuleId.HasValue) return config;

        var rule = await _dbContext.Set<CfPluginRule>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FID == context.PluginRuleId.Value);

        if (rule?.F规则配置JSON == null) return config;

        var root = JsonDocument.Parse(rule.F规则配置JSON).RootElement;

        if (root.TryGetProperty("schemeId", out var sid))
        {
            if (sid.ValueKind == JsonValueKind.String && Guid.TryParse(sid.GetString(), out var guid))
                config.SchemeId = guid;
        }
        if (root.TryGetProperty("voucherDateField", out var vdf) && vdf.ValueKind == JsonValueKind.String)
            config.VoucherDateField = vdf.GetString() ?? config.VoucherDateField;
        if (root.TryGetProperty("voucherNoField", out var vnf) && vnf.ValueKind == JsonValueKind.String)
            config.VoucherNoField = vnf.GetString() ?? config.VoucherNoField;
        if (root.TryGetProperty("voucherWordField", out var vwf) && vwf.ValueKind == JsonValueKind.String)
            config.VoucherWordField = vwf.GetString() ?? config.VoucherWordField;
        if (root.TryGetProperty("summaryField", out var sf) && sf.ValueKind == JsonValueKind.String)
            config.SummaryField = sf.GetString() ?? config.SummaryField;
        if (root.TryGetProperty("accountCodeField", out var acf) && acf.ValueKind == JsonValueKind.String)
            config.AccountCodeField = acf.GetString() ?? config.AccountCodeField;
        if (root.TryGetProperty("debitField", out var df) && df.ValueKind == JsonValueKind.String)
            config.DebitField = df.GetString() ?? config.DebitField;
        if (root.TryGetProperty("creditField", out var cf) && cf.ValueKind == JsonValueKind.String)
            config.CreditField = cf.GetString() ?? config.CreditField;
        if (root.TryGetProperty("auxiliaryField", out var af) && af.ValueKind == JsonValueKind.String)
            config.AuxiliaryField = af.GetString();
        if (root.TryGetProperty("assetCodeField", out var asf) && asf.ValueKind == JsonValueKind.String)
            config.AssetCodeField = asf.GetString();
        if (root.TryGetProperty("failOnMissing", out var fm))
        {
            if (fm.ValueKind == JsonValueKind.True) config.FailOnMissing = true;
            else if (fm.ValueKind == JsonValueKind.False) config.FailOnMissing = false;
        }

        // 辅助核算格式配置
        if (root.TryGetProperty("auxFormat", out var auxFmt) && auxFmt.ValueKind == JsonValueKind.Object)
        {
            config.AuxFormat = new AuxFormatConfig();
            if (auxFmt.TryGetProperty("auxMode", out var am) && am.ValueKind == JsonValueKind.String)
                config.AuxFormat.AuxMode = am.GetString() ?? "single";
            if (auxFmt.TryGetProperty("separator", out var sep) && sep.ValueKind == JsonValueKind.String)
                config.AuxFormat.Separator = sep.GetString() ?? ";";
            if (auxFmt.TryGetProperty("kvSeparator", out var kvs) && kvs.ValueKind == JsonValueKind.String)
                config.AuxFormat.KvSeparator = kvs.GetString() ?? ":";
            if (auxFmt.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Array)
            {
                config.AuxFormat.Columns = new List<AuxColumnDef>();
                foreach (var col in cols.EnumerateArray())
                {
                    var colDef = new AuxColumnDef();
                    if (col.TryGetProperty("field", out var fld) && fld.ValueKind == JsonValueKind.String)
                        colDef.Field = fld.GetString() ?? "";
                    if (col.TryGetProperty("type", out var tp) && tp.ValueKind == JsonValueKind.String)
                        colDef.Type = tp.GetString() ?? "";
                    config.AuxFormat.Columns.Add(colDef);
                }
            }
        }

        return config;
    }

    private static string? GetFieldValue(IDictionary<string, object> row, string? fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return null;
        if (!row.TryGetValue(fieldName, out var val)) return null;
        if (val == null || val == DBNull.Value) return null;
        return val.ToString();
    }

    private static decimal GetDecimalValue(IDictionary<string, object> row, string? fieldName)
    {
        var str = GetFieldValue(row, fieldName);
        if (string.IsNullOrEmpty(str)) return 0m;
        return decimal.TryParse(str, out var d) ? d : 0m;
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        return DateTime.TryParse(dateStr, out var dt) ? dt : null;
    }

    private string? ResolveAuxiliaryFromRow(
        IDictionary<string, object> row,
        VoucherMigrationConfig config,
        Dictionary<(string, string), FinAuxMappingDetail> auxMappings,
        string defaultStrategy,
        Dictionary<string, int> missingAux)
    {
        if (config.AuxFormat?.AuxMode == "multi" && config.AuxFormat.Columns is { Count: > 0 })
        {
            var auxList = new List<object>();
            foreach (var colDef in config.AuxFormat.Columns)
            {
                var sourceValue = GetFieldValue(row, colDef.Field);
                if (string.IsNullOrEmpty(sourceValue)) continue;

                try
                {
                    // 方案B 源打标(D)：业务方向直接归一为 OUT/IN/CMB，不走映射表
                    var targetCode = colDef.Type == AuxTypes.BusinessDirection
                        ? BusinessDirection.Normalize(sourceValue)
                        : _mappingService.ResolveAuxiliary(colDef.Type, sourceValue, auxMappings, defaultStrategy);
                    if (targetCode != null)
                    {
                        auxList.Add(new { type = colDef.Type, code = targetCode, name = sourceValue });
                    }
                }
                catch
                {
                    var key = $"{colDef.Type}:{sourceValue}";
                    missingAux[key] = missingAux.GetValueOrDefault(key) + 1;
                }
            }
            return auxList.Count > 0 ? JsonSerializer.Serialize(auxList) : null;
        }
        else
        {
            var rawValue = GetFieldValue(row, config.AuxiliaryField);
            if (string.IsNullOrEmpty(rawValue)) return null;

            var separator = config.AuxFormat?.Separator ?? ";";
            var kvSeparator = config.AuxFormat?.KvSeparator ?? ":";
            var parts = rawValue.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var auxList = new List<object>();
            foreach (var part in parts)
            {
                var kv = part.Split(kvSeparator, 2, StringSplitOptions.TrimEntries);
                if (kv.Length < 2) continue;

                var auxType = kv[0];
                var sourceCode = kv[1];

                try
                {
                    // 方案B 源打标(D)：业务方向直接归一为 OUT/IN/CMB，不走映射表
                    var targetCode = auxType == AuxTypes.BusinessDirection
                        ? BusinessDirection.Normalize(sourceCode)
                        : _mappingService.ResolveAuxiliary(auxType, sourceCode, auxMappings, defaultStrategy);
                    if (targetCode != null)
                    {
                        auxList.Add(new { type = auxType, code = targetCode, name = sourceCode });
                    }
                }
                catch
                {
                    var key = $"{auxType}:{sourceCode}";
                    missingAux[key] = missingAux.GetValueOrDefault(key) + 1;
                }
            }
            return auxList.Count > 0 ? JsonSerializer.Serialize(auxList) : null;
        }
    }

    private static string BuildMissingReport(
        Dictionary<string, int> missingAccounts,
        Dictionary<string, int> missingAux,
        Dictionary<string, int> missingAssets,
        int voucherCount,
        int skippedRows,
        int totalRows)
    {
        var parts = new List<string>
        {
            $"处理完成: 总行数={totalRows}, 跳过={skippedRows}, 生成凭证={voucherCount}张"
        };

        if (missingAccounts.Count > 0)
        {
            var top5 = missingAccounts.OrderByDescending(x => x.Value).Take(5)
                .Select(x => $"{x.Key}({x.Value}次)");
            parts.Add($"缺失科目映射({missingAccounts.Count}种): {string.Join(", ", top5)}");
        }

        if (missingAux.Count > 0)
        {
            var top5 = missingAux.OrderByDescending(x => x.Value).Take(5)
                .Select(x => $"{x.Key}({x.Value}次)");
            parts.Add($"缺失辅助映射({missingAux.Count}种): {string.Join(", ", top5)}");
        }

        if (missingAssets.Count > 0)
        {
            var top5 = missingAssets.OrderByDescending(x => x.Value).Take(5)
                .Select(x => $"{x.Key}({x.Value}次)");
            parts.Add($"缺失资产映射({missingAssets.Count}种): {string.Join(", ", top5)}");
        }

        return string.Join("; ", parts);
    }

    #endregion

    #region 内部类型

    private class VoucherMigrationConfig
    {
        public Guid SchemeId { get; set; }
        public string VoucherDateField { get; set; } = "F凭证日期";
        public string VoucherNoField { get; set; } = "F凭证号";
        public string VoucherWordField { get; set; } = "F凭证字";
        public string SummaryField { get; set; } = "F摘要";
        public string AccountCodeField { get; set; } = "F科目编码";
        public string DebitField { get; set; } = "F借方金额";
        public string CreditField { get; set; } = "F贷方金额";
        public string? AuxiliaryField { get; set; }
        public string? AssetCodeField { get; set; }
        public bool FailOnMissing { get; set; } = true;
        public AuxFormatConfig? AuxFormat { get; set; }
    }

    private class AuxFormatConfig
    {
        public string AuxMode { get; set; } = "single";
        public string Separator { get; set; } = ";";
        public string KvSeparator { get; set; } = ":";
        public List<AuxColumnDef>? Columns { get; set; }
    }

    private class AuxColumnDef
    {
        public string Field { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    private class VoucherGroupData
    {
        public DateTime? VoucherDate { get; set; }
        public string VoucherWord { get; set; } = "记";
        public List<EntryData> Entries { get; set; } = new();
    }

    private class EntryData
    {
        public long TargetAccountId { get; set; }
        public string TargetAccountCode { get; set; } = string.Empty;
        public string TargetAccountName { get; set; } = string.Empty;
        public string? AuxJson { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public string Summary { get; set; } = string.Empty;
        public long? AssetCardId { get; set; }
    }

    #endregion
}
