using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// 成本计算引擎（独立于价格计算，读取已计费运单 → 计算成本 → 写入明细表）
/// </summary>
public class CostEngine
{
    private readonly IRepository<ExpCostPlan> _costPlanRepo;
    private readonly IRepository<ExpCostItem> _costItemRepo;
    private readonly BillingBulkWriter _bulkWriter;
    private readonly IProgressNotifier _progressNotifier;
    private readonly ILogger<CostEngine> _logger;

    public CostEngine(
        IRepository<ExpCostPlan> costPlanRepo,
        IRepository<ExpCostItem> costItemRepo,
        BillingBulkWriter bulkWriter,
        IProgressNotifier progressNotifier,
        ILogger<CostEngine> logger)
    {
        _costPlanRepo = costPlanRepo;
        _costItemRepo = costItemRepo;
        _bulkWriter = bulkWriter;
        _progressNotifier = progressNotifier;
        _logger = logger;
    }

    /// <summary>
    /// 验证表名只包含合法字符（防SQL注入）
    /// </summary>
    private static void ValidateTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空", nameof(tableName));
        if (!Regex.IsMatch(tableName, @"^[\w\u4e00-\u9fff]+$"))
            throw new ArgumentException($"非法表名: {tableName}", nameof(tableName));
    }

    /// <summary>
    /// 执行成本计算：对已计费成功的运单计算成本并写入明细表
    /// </summary>
    /// <param name="waybills">已计费成功的运单列表（需包含 NetworkPointId、BrandCode、DestinationProvinceId、BillableWeight 等）</param>
    /// <param name="resultTable">计费结果主表名（用于关联查询 BillingResultId）</param>
    /// <param name="orgId">组织ID，用于设置成本明细的组织归属</param>
    /// <returns>执行结果</returns>
    public async Task<CostExecutionResult> ExecuteAsync(
        List<CostWaybillInput> waybills,
        string resultTable,
        long orgId,
        long batchId)
    {
        ValidateTableName(resultTable);
        var costTable = resultTable + "_成本明细";
        ValidateTableName(costTable);
        var sw = Stopwatch.StartNew();

        // === 第一阶段：加载成本缓存 ===
        // 注意：不能用 Task.WhenAll，因为所有 IRepository 共享同一个 scoped DbContext，并发访问会抛异常
        var costCache = new CostPlanCache();
        await costCache.LoadAsync(_costPlanRepo, _costItemRepo, orgId);

        _logger.LogWarning("CostEngine: 缓存加载完成 - PlanCount={PlanCount}, SegmentCount={SegmentCount}, OrgId={OrgId}",
            costCache.PlanCount, costCache.SegmentCount, orgId);

        // 成本项名称跨表关联（全局项目 FName ←→ 方案项 FItemName）的脏数据告警，便于发现配置问题：
        // 未匹配上的方案项会丢失返利标志（返利金额被当成正向成本）；规范化后重名的全局项只有首个生效。
        if (costCache.UnmatchedCostItemNames.Count > 0)
            _logger.LogWarning("CostEngine: {Count} 个方案成本项名称匹配不到全局成本项目（返利标志将缺失，请核对名称）: {Names}",
                costCache.UnmatchedCostItemNames.Count, string.Join(" | ", costCache.UnmatchedCostItemNames));
        if (costCache.DuplicateGlobalItemNames.Count > 0)
            _logger.LogWarning("CostEngine: {Count} 个全局成本项目规范化后重名（仅首个生效，可能张冠李戴，请去重）: {Names}",
                costCache.DuplicateGlobalItemNames.Count, string.Join(" | ", costCache.DuplicateGlobalItemNames));

        if (costCache.PlanCount == 0 || costCache.SegmentCount == 0)
        {
            sw.Stop();
            return new CostExecutionResult
            {
                TotalWaybills = waybills.Count,
                SuccessCount = 0,
                ErrorCount = waybills.Count,
                CostBreakdownsCreated = 0,
                Duration = sw.Elapsed,
                Errors = new List<CostError>
                {
                    new()
                    {
                        WaybillId = waybills.FirstOrDefault()?.RowId ?? 0,
                        WaybillNo = waybills.FirstOrDefault()?.WaybillNo ?? string.Empty,
                        ErrorMessage = "未找到启用中的成本方案或成本矩阵，请先启用成本方案并维护成本项矩阵"
                    }
                }
            };
        }

        // === 第二阶段：并行计算成本 ===
        var costDataMap = new ConcurrentDictionary<long, (List<(int CostItemId, decimal Amount)> Costs, int CostMode)>();
        var errors = new ConcurrentBag<CostError>();
        int successCount = 0;
        int processedCount = 0;
        int totalWaybillCount = waybills.Count;

        Parallel.ForEach(waybills, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
        waybill =>
        {
            try
            {
                var calcResult = costCache.CalcAllCosts(
                    waybill.NetworkPointId, waybill.BrandCode,
                    waybill.DestinationProvinceId, waybill.DestinationCityName,
                    waybill.BillableWeight, waybill.WaybillDate, waybill.ShopName);
                var costList = calcResult.Items.Select(c => (c.CostItemId, c.Amount)).ToList();

                if (costList.Count == 0)
                {
                    errors.Add(new CostError
                    {
                        WaybillId = waybill.RowId,
                        WaybillNo = waybill.WaybillNo,
                        ErrorMessage = $"未匹配到成本项：品牌={waybill.BrandCode}, 目的省份ID={waybill.DestinationProvinceId}, 重量={waybill.BillableWeight}, 日期={waybill.WaybillDate:yyyy-MM-dd}, 模式={(calcResult.CostMode == 2 ? "一口价" : "标准")}"
                    });
                    return;
                }

                costDataMap[waybill.RowId] = (costList, calcResult.CostMode);
                Interlocked.Increment(ref successCount);
            }
            catch (Exception ex)
            {
                errors.Add(new CostError
                {
                    WaybillId = waybill.RowId,
                    WaybillNo = waybill.WaybillNo,
                    ErrorMessage = ex.Message
                });
            }
            finally
            {
                var current = Interlocked.Increment(ref processedCount);
                _progressNotifier.NotifyAutoPluginDataProgressAsync(batchId, "成本计算", current, totalWaybillCount, "计算成本")
                    .GetAwaiter()
                    .GetResult();
            }
        });

        // === 第三阶段：查询 BillingResultId 映射并批量写入 ===
        int costBreakdownCount = 0;
        if (costDataMap.Count > 0)
        {
            try
            {
                await _progressNotifier.NotifyAutoPluginDataProgressAsync(
                    batchId, "成本计算", totalWaybillCount, totalWaybillCount, "写入成本明细");

                using var connection = new SqlConnection(_bulkWriter.GetConnectionString());
                await connection.OpenAsync();
                using var dbTransaction = (SqlTransaction)await connection.BeginTransactionAsync();

                try
                {
                    // 查回 Level 0 的 BillingResultId 映射
                    var waybillIds = costDataMap.Keys.ToList();
                    var waybillToResultId = await QueryBillingResultIds(
                        waybillIds, resultTable, connection, dbTransaction);
                    // RowId 即计费结果 FID，成本计算模式随行回写
                    var resultIdToCostMode = costDataMap.ToDictionary(
                        pair => waybillToResultId[pair.Key],
                        pair => pair.Value.CostMode);
                    var billingResultIds = waybillToResultId.Values.Distinct().ToList();

                    // 构建成本明细
                    var costBreakdowns = new List<ExpBillingCostBreakdown>();
                    foreach (var (waybillId, payload) in costDataMap)
                    {
                        if (waybillToResultId.TryGetValue(waybillId, out var billingResultId))
                        {
                            foreach (var (costItemId, amount) in payload.Costs)
                            {
                                costBreakdowns.Add(new ExpBillingCostBreakdown
                                {
                                    FBillingResultId = billingResultId,
                                    FCostItemId = costItemId,
                                    FAmount = amount,
                                    FOrgId = orgId
                                });
                            }
                        }
                    }

                    if (costBreakdowns.Count > 0)
                    {
                        await CreateBillingResultIdTempTable(connection, dbTransaction, billingResultIds, resultIdToCostMode);

                        var deleteExistingSql = $@"
                            DELETE c
                            FROM [{costTable}] c
                            INNER JOIN #CostBillingResultIds ids ON ids.[FID] = c.[F计费结果ID]";
                        using (var deleteCmd = new SqlCommand(deleteExistingSql, connection, dbTransaction))
                        {
                            deleteCmd.CommandTimeout = 120;
                            await deleteCmd.ExecuteNonQueryAsync();
                        }

                        await _bulkWriter.BulkInsertCostBreakdowns(
                            costBreakdowns, costTable, connection, dbTransaction);
                        costBreakdownCount = costBreakdowns.Count;

                        // 回写 F成本合计 与 F成本计算模式（1=标准 2=一口价）到计费结果主表
                        var updateTotalCostSql = $@"
                            UPDATE r SET r.[F成本合计] = ISNULL(t.TotalCost, 0),
                                         r.[F成本计算模式] = ids.[CostMode]
                            FROM [{resultTable}] r
                            INNER JOIN #CostBillingResultIds ids ON ids.[FID] = r.[FID]
                            LEFT JOIN (
                                SELECT c.[F计费结果ID], SUM(c.[F金额]) AS TotalCost
                                FROM [{costTable}] c
                                INNER JOIN #CostBillingResultIds i ON i.[FID] = c.[F计费结果ID]
                                GROUP BY c.[F计费结果ID]
                            ) t ON r.[FID] = t.[F计费结果ID]";
                        using var updateCmd = new SqlCommand(updateTotalCostSql, connection, dbTransaction);
                        updateCmd.CommandTimeout = 120;
                        await updateCmd.ExecuteNonQueryAsync();
                    }

                    await dbTransaction.CommitAsync();
                }
                catch
                {
                    await dbTransaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CostEngine: 成本明细写入失败");
                throw;
            }
        }

        sw.Stop();

        return new CostExecutionResult
        {
            TotalWaybills = waybills.Count,
            SuccessCount = successCount,
            ErrorCount = errors.Count,
            CostBreakdownsCreated = costBreakdownCount,
            Duration = sw.Elapsed,
            Errors = errors.ToList()
        };
    }

    /// <summary>
    /// 查询 Level 0 的 BillingResultId 映射（WaybillNo → FID）
    /// </summary>
    private static async Task CreateBillingResultIdTempTable(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyCollection<long> billingResultIds,
        IReadOnlyDictionary<long, int> resultIdToCostMode)
    {
        using (var createCmd = new SqlCommand(
            "CREATE TABLE #CostBillingResultIds ([FID] BIGINT NOT NULL PRIMARY KEY, [CostMode] INT NOT NULL)",
            connection,
            transaction))
        {
            await createCmd.ExecuteNonQueryAsync();
        }

        var table = new DataTable();
        table.Columns.Add("FID", typeof(long));
        table.Columns.Add("CostMode", typeof(int));
        foreach (var id in billingResultIds)
            table.Rows.Add(id, resultIdToCostMode.GetValueOrDefault(id, 1));

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
        {
            DestinationTableName = "#CostBillingResultIds",
            BatchSize = 5000
        };
        bulkCopy.ColumnMappings.Add("FID", "FID");
        bulkCopy.ColumnMappings.Add("CostMode", "CostMode");
        await bulkCopy.WriteToServerAsync(table);
    }

    private static async Task<Dictionary<long, long>> QueryBillingResultIds(
        List<long> waybillIds, string resultTable, SqlConnection connection, SqlTransaction transaction)
    {
        // RowId 来自 CostAgent.ReadSuccessBillingResults 的 SELECT r.FID，
        // 即 RowId 本身就是计费结果表的 FID (BillingResultId)，直接自映射即可。
        return waybillIds.ToDictionary(id => id, id => id);
    }
}

/// <summary>
/// 成本计算运单输入
/// </summary>
public class CostWaybillInput
{
    /// <summary>运单行ID（STG表FID）</summary>
    public long RowId { get; set; }
    /// <summary>运单编号</summary>
    public string WaybillNo { get; set; } = string.Empty;
    /// <summary>归属网点ID</summary>
    public long NetworkPointId { get; set; }
    /// <summary>品牌编码</summary>
    public string BrandCode { get; set; } = string.Empty;
    /// <summary>目的省份ID</summary>
    public int DestinationProvinceId { get; set; }
    /// <summary>目的城市名称</summary>
    public string? DestinationCityName { get; set; }
    /// <summary>计费重量</summary>
    public decimal BillableWeight { get; set; }
    /// <summary>运单日期</summary>
    public DateTime WaybillDate { get; set; }
    /// <summary>运单店铺名称（用于一口价匹配，来源于 STG.F店铺账号）</summary>
    public string? ShopName { get; set; }
}

/// <summary>
/// 成本计算执行结果
/// </summary>
public class CostExecutionResult
{
    public int TotalWaybills { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public int CostBreakdownsCreated { get; set; }
    public TimeSpan Duration { get; set; }
    public List<CostError> Errors { get; set; } = new();
}

/// <summary>
/// 成本计算错误
/// </summary>
public class CostError
{
    public long WaybillId { get; set; }
    public string WaybillNo { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
