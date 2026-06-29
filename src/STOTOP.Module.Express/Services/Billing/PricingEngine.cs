using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;


namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// 价格计算引擎（仅负责报价计算，不含成本计算）
/// 6步骤固定模型：KH→DL→WD→YW→CB→YZ
/// </summary>
public class PricingEngine
{
    private static readonly string[] ClientTypeSteps = { "KH", "DL", "WD", "YW", "CB", "YZ" };

    private readonly IRepository<ExpQuotation> _quotationRepo;
    private readonly IRepository<ExpQuotationShop> _quotationShopRepo;
    private readonly IRepository<ExpQuotationCommission> _commissionRepo;
    private readonly IRepository<ExpQuotationAlias> _aliasRepo;
    private readonly IRepository<ExpNetworkPoint> _networkPointRepo;
    private readonly IRepository<ExpPriceSurcharge> _surchargeRepo;
    private readonly IRepository<ExpPriceSurchargeItem> _surchargeItemRepo;
    private readonly IRepository<ExpPriceSurchargeItemDest> _surchargeDestRepo;
    private readonly IRepository<ExpAgent> _agentRepo;
    private readonly IRepository<ExpSalesman> _salesmanRepo;
    private readonly IRepository<ExpFranchiseArea> _franchiseAreaRepo;
    private readonly IRepository<ExpLastMileStation> _stationRepo;
    private readonly BillingBulkWriter _bulkWriter;
    private readonly ILogger<PricingEngine> _logger;

    public PricingEngine(
        IRepository<ExpQuotation> quotationRepo,
        IRepository<ExpQuotationShop> quotationShopRepo,
        IRepository<ExpQuotationCommission> commissionRepo,
        IRepository<ExpQuotationAlias> aliasRepo,
        IRepository<ExpNetworkPoint> networkPointRepo,
        IRepository<ExpPriceSurcharge> surchargeRepo,
        IRepository<ExpPriceSurchargeItem> surchargeItemRepo,
        IRepository<ExpPriceSurchargeItemDest> surchargeDestRepo,
        IRepository<ExpAgent> agentRepo,
        IRepository<ExpSalesman> salesmanRepo,
        IRepository<ExpFranchiseArea> franchiseAreaRepo,
        IRepository<ExpLastMileStation> stationRepo,
        BillingBulkWriter bulkWriter,
        ILogger<PricingEngine> logger)
    {
        _quotationRepo = quotationRepo;
        _quotationShopRepo = quotationShopRepo;
        _commissionRepo = commissionRepo;
        _aliasRepo = aliasRepo;
        _networkPointRepo = networkPointRepo;
        _surchargeRepo = surchargeRepo;
        _surchargeItemRepo = surchargeItemRepo;
        _surchargeDestRepo = surchargeDestRepo;
        _agentRepo = agentRepo;
        _salesmanRepo = salesmanRepo;
        _franchiseAreaRepo = franchiseAreaRepo;
        _stationRepo = stationRepo;
        _bulkWriter = bulkWriter;
        _logger = logger;
    }

    /// <summary>
    /// 验证表名只包含合法字符（防SQL注入）
    /// </summary>
    private static void ValidateTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空", nameof(tableName));
        // 允许中文、字母、数字、下划线
        if (!Regex.IsMatch(tableName, @"^[\w\u4e00-\u9fff]+$"))
            throw new ArgumentException($"非法表名: {tableName}", nameof(tableName));
    }

    /// <summary>
    /// 执行价格计算任务，接收标准化的运单列表
    /// </summary>
    public async Task<BillingExecutionResult> ExecuteAsync(
        List<BillingWaybillData> waybills,
        string sourceTable,
        long batchId,
        string resultTable,
        IProgressNotifier? progressNotifier = null)
    {
        ValidateTableName(sourceTable);
        ValidateTableName(resultTable);
        var sw = Stopwatch.StartNew();

        // 组织ID来自运单（PricingPlugin 已统一赋值）。用于参考数据缓存的显式组织过滤，
        // 防止后台链路组织过滤器失效时跨组织串报价/价格/加收。
        var orgId = waybills.FirstOrDefault()?.OrgId ?? 0;

        // === 第一阶段：缓存构建 ===
        var quotationCache = new QuotationLookupCache();
        await quotationCache.LoadAsync(_quotationRepo, _quotationShopRepo, _networkPointRepo, _commissionRepo, _aliasRepo, _agentRepo, _salesmanRepo, _franchiseAreaRepo, _stationRepo, orgId);
        _logger.LogWarning("PricingEngine 缓存诊断: 报价={QuotationCount}, 店铺关联={ShopLinkCount}, 唯一店铺={UniqueShopCount}",
            quotationCache.DiagQuotationCount, quotationCache.DiagShopLinkCount, quotationCache.DiagUniqueShopCount);

        var priceCache = new PricePlanCache();
        await priceCache.LoadAsync(_quotationRepo, orgId);

        var surchargeCache = new SurchargeCache();
        await surchargeCache.LoadAsync(_surchargeRepo, _surchargeItemRepo, _surchargeDestRepo, orgId);

        // === 预计算每日单量（用于单量加收附加费 Type=6） ===
        // 注意：此处单量按"本批次"内的运单统计。若同一店铺同一天的运单被拆到多个导入批次，
        //       各批次会各自计数，单量阶梯加收可能命中错档。常规用法（同一天数据一个批次导入）下结果正确。
        var dailyVolumeMap = waybills
            .Where(w => !string.IsNullOrEmpty(w.ShopName))
            .GroupBy(w => (w.ShopName, w.WaybillDate.Date))
            .ToDictionary(
                g => g.Key,
                g => g.Count()
            );

        // === 第二阶段：并行计费 ===
        var billingResults = new ConcurrentBag<ExpBillingResult>();
        var errors = new ConcurrentBag<BillingError>();
        var npMismatches = new ConcurrentBag<NetworkPointMismatchInfo>();
        int successCount = 0;
        int processedCount = 0;
        int totalWaybillCount = waybills.Count;

        Parallel.ForEach(waybills, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
        waybill =>
        {
            try
            {
                ProcessSingleWaybill(waybill, quotationCache, priceCache, surchargeCache, dailyVolumeMap, billingResults, npMismatches);
                waybill.BillingStatus = 1;
                Interlocked.Increment(ref successCount);
            }
            catch (BillingException ex)
            {
                waybill.BillingStatus = 2;
                errors.Add(new BillingError
                {
                    WaybillId = waybill.RowId,
                    WaybillNo = waybill.WaybillNo,
                    ShopName = waybill.ShopName,
                    ErrorCode = ex.ErrorCode,
                    ErrorMessage = ex.Message
                });
                billingResults.Add(new ExpBillingResult
                {
                    FWaybillNo = waybill.WaybillNo,
                    FWaybillDate = waybill.WaybillDate,
                    FPartyClientId = string.Empty,
                    FPartyClientName = string.Empty,
                    FCalcStatus = 2,
                    FErrorMessage = $"{ex.ErrorCode}: {ex.Message}",
                    FBillingDate = DateTime.Today,
                    FOrgId = waybill.OrgId,
                    FNetworkPointCode = waybill.NetworkPointCode,
                });
            }
            catch (Exception ex)
            {
                waybill.BillingStatus = 2;
                errors.Add(new BillingError
                {
                    WaybillId = waybill.RowId,
                    WaybillNo = waybill.WaybillNo,
                    ShopName = waybill.ShopName,
                    ErrorCode = "ERR_UNKNOWN",
                    ErrorMessage = ex.Message
                });
                billingResults.Add(new ExpBillingResult
                {
                    FWaybillNo = waybill.WaybillNo,
                    FWaybillDate = waybill.WaybillDate,
                    FPartyClientId = string.Empty,
                    FPartyClientName = string.Empty,
                    FCalcStatus = 2,
                    FErrorMessage = $"ERR_UNKNOWN: {ex.Message}",
                    FBillingDate = DateTime.Today,
                    FOrgId = waybill.OrgId,
                    FNetworkPointCode = waybill.NetworkPointCode,
                });
            }
            finally
            {
                var current = Interlocked.Increment(ref processedCount);
                if (progressNotifier != null)
                {
                    // 节流由 NotifyAutoPluginDataProgressAsync 内部处理，此处安全地同步调用
                    progressNotifier.NotifyAutoPluginDataProgressAsync(batchId, "价格计算", current, totalWaybillCount, "计算运费")
                        .GetAwaiter().GetResult();
                }
            }
        });

        // === 第三阶段：批量写入（双阶段事务保护） ===
        var resultList = billingResults.ToList();

        // 统一赋值 batchId
        foreach (var r in resultList)
            r.FBatchId = batchId;

        var successResults = resultList.Where(r => r.FCalcStatus == 1).ToList();
        var failureResults = resultList.Where(r => r.FCalcStatus == 2).ToList();

        // 分离成功和失败的运单列表（用于BulkUpdateStgStatus）
        var successWaybills = waybills.Where(w => w.BillingStatus == 1).ToList();
        var failureWaybills = waybills.Where(w => w.BillingStatus == 2).ToList();

        bool mainTxnFailed = false;
        Exception? mainTxnError = null;

        // Phase A: 主事务 — 成功记录
        if (successResults.Count > 0)
        {
            using var connection = new SqlConnection(_bulkWriter.GetConnectionString());
            await connection.OpenAsync();
            using var dbTransaction = (SqlTransaction)await connection.BeginTransactionAsync();
            try
            {
                // 重跑去重：先删除这批运单的旧计费结果，再插入新结果
                var successWaybillNos = successResults.Select(r => r.FWaybillNo!).Distinct().ToList();
                await _bulkWriter.DeleteExistingResults(
                    successWaybillNos, resultTable, batchId, connection, dbTransaction);

                await _bulkWriter.BulkInsertBillingResults(
                    successResults, resultTable, connection, dbTransaction);

                // 只更新成功运单的STG状态
                if (successWaybills.Count > 0)
                {
                    await BulkUpdateStgStatus(successWaybills, sourceTable, connection, dbTransaction);
                }

                await dbTransaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                mainTxnFailed = true;
                mainTxnError = ex;
                _logger.LogError(ex, "PricingEngine: 成功记录写入事务失败，已回滚");
            }
        }

        // Phase B: 独立持久化 — 失败记录（始终执行，不受Phase A影响）
        bool failureSaved = false;
        if (failureResults.Count > 0)
        {
            const int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var errConnection = new SqlConnection(_bulkWriter.GetConnectionString());
                    await errConnection.OpenAsync();
                    using var errTransaction = (SqlTransaction)await errConnection.BeginTransactionAsync();
                    try
                    {
                        // 重跑去重：先删除这批运单的旧失败记录（仅限本批次失败状态行）。
                        // 已知边角：某运单上一轮全部成功、本轮全部失败时，Phase A 不处理它、
                        // Phase B 只删状态=2，旧成功行会残留。极少见，详见
                        // docs/superpowers/specs/2026-06-14-express-billing-delete-batch-scope-design.md。
                        var failureWaybillNos = failureResults.Select(r => r.FWaybillNo!).Distinct().ToList();
                        await _bulkWriter.DeleteExistingResults(
                            failureWaybillNos, resultTable, batchId, errConnection, errTransaction, calcStatus: 2);

                        await _bulkWriter.BulkInsertBillingResults(
                            failureResults, resultTable, errConnection, errTransaction);

                        if (failureWaybills.Count > 0)
                        {
                            await BulkUpdateStgStatus(
                                failureWaybills, sourceTable, errConnection, errTransaction);
                        }

                        await errTransaction.CommitAsync();
                        _logger.LogInformation(
                            "PricingEngine: 失败记录独立持久化完成，共{Count}条", failureResults.Count);
                    }
                    catch
                    {
                        await errTransaction.RollbackAsync();
                        throw;
                    }

                    failureSaved = true;
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "失败记录写入第{Attempt}次尝试失败，共{MaxRetries}次", attempt, maxRetries);
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(attempt * 500); // 指数退避: 500ms, 1000ms
                    }
                    else
                    {
                        _logger.LogError(ex, "失败记录写入已耗尽所有重试次数({MaxRetries}次)，{Count}条失败记录未保存", maxRetries, failureResults.Count);
                    }
                    // 不抛出，确保不影响主流程
                }
            }
        }

        // 如果主事务失败，抛出原始异常
        if (mainTxnFailed && mainTxnError != null)
        {
            throw mainTxnError;
        }

        sw.Stop();

        var result = new BillingExecutionResult
        {
            TotalWaybills = waybills.Count,
            SuccessCount = successCount,
            ErrorCount = errors.Count,
            BillingResultsCreated = resultList.Count,
            Duration = sw.Elapsed,
            Errors = errors.ToList(),
            NetworkPointMismatches = npMismatches.ToList()
        };

        if (!failureSaved && failureResults.Count > 0)
        {
            result.FailureRecordSaveError = true;
            result.FailureRecordUnsavedCount = failureResults.Count;
        }

        return result;
    }

    /// <summary>
    /// 解释单票价格计算，不写入计费结果，用于导入计算验证工作台定位数据、配置和公式问题。
    /// </summary>
    public async Task<PricingEngineExplainResult> ExplainAsync(
        BillingWaybillData waybill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(waybill);
        var results = await ExplainBatchAsync([waybill], dailyVolumeMap: null, cancellationToken);
        return results[0];
    }

    /// <summary>
    /// 批量解释价格计算（参考数据缓存只加载一次），不写入计费结果。
    /// dailyVolumeMap 为空时按入参运单自行统计单量（票量阶梯附加费可能与整批计费时存在偏差）。
    /// </summary>
    public async Task<List<PricingEngineExplainResult>> ExplainBatchAsync(
        List<BillingWaybillData> waybills,
        Dictionary<(string ShopName, DateTime Date), int>? dailyVolumeMap = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(waybills);
        cancellationToken.ThrowIfCancellationRequested();

        if (waybills.Count == 0)
            return [];

        var orgId = waybills[0].OrgId;

        var quotationCache = new QuotationLookupCache();
        await quotationCache.LoadAsync(_quotationRepo, _quotationShopRepo, _networkPointRepo, _commissionRepo, _aliasRepo, _agentRepo, _salesmanRepo, _franchiseAreaRepo, _stationRepo, orgId);

        var priceCache = new PricePlanCache();
        await priceCache.LoadAsync(_quotationRepo, orgId);

        var surchargeCache = new SurchargeCache();
        await surchargeCache.LoadAsync(_surchargeRepo, _surchargeItemRepo, _surchargeDestRepo, orgId);

        if (dailyVolumeMap == null)
        {
            dailyVolumeMap = new Dictionary<(string ShopName, DateTime Date), int>();
            foreach (var w in waybills)
            {
                if (string.IsNullOrWhiteSpace(w.ShopName))
                    continue;
                var key = (w.ShopName, w.WaybillDate.Date);
                dailyVolumeMap[key] = dailyVolumeMap.GetValueOrDefault(key) + 1;
            }
        }

        var results = new List<PricingEngineExplainResult>(waybills.Count);
        foreach (var waybill in waybills)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(ExplainSingleWaybill(waybill, quotationCache, priceCache, surchargeCache, dailyVolumeMap));
        }

        return results;
    }

    /// <summary>
    /// 6步骤固定模型：按 KH→DL→WD→YW→CB→YZ 顺序查找报价并计费
    /// </summary>
    private void ProcessSingleWaybill(
        BillingWaybillData waybill,
        QuotationLookupCache quotationCache,
        PricePlanCache priceCache,
        SurchargeCache surchargeCache,
        Dictionary<(string ShopName, DateTime Date), int> dailyVolumeMap,
        ConcurrentBag<ExpBillingResult> billingResults,
        ConcurrentBag<NetworkPointMismatchInfo> npMismatches)
    {
        int stepIndex = 0;
        bool hasAnyResult = false;
        bool mismatchRecorded = false;

        foreach (var clientType in ClientTypeSteps)
        {
            // FindQuotation 内部处理共享店铺逻辑：
            //   非共享报价 → 直接匹配
            //   共享报价 → 检查 waybill.ClientAlias 是否在报价的别名列表中
            var quotation = quotationCache.FindQuotation(
                waybill.ShopName, waybill.BrandCode, clientType, waybill.WaybillDate,
                alias: waybill.ClientAlias);

            if (quotation == null) { stepIndex++; continue; }

            // 根据报价方案的结算重量环节取值
            var billableWeight = GetBillingWeight(waybill, quotation.SettlementWeightStage)
                ?? throw new BillingException("ERR_NO_WEIGHT", "无有效重量");

            // 查重量段
            var segment = priceCache.FindSegment(quotation.QuotationId, billableWeight);
            if (segment == null) { stepIndex++; continue; }

            // 查价格矩阵
            var provinceId = waybill.DestinationProvinceId ?? 0;
            var cell = priceCache.FindCell(quotation.QuotationId, segment.SegmentIndex, provinceId, waybill.DestinationCityName)
                ?? throw new BillingException("ERR_NO_PRICE_CELL",
                    $"价格矩阵缺失: Quotation={quotation.PlanCode}, Type={clientType}");

            // 计价（统一公式：合并进位 + 计算）
            var freight = PriceFormula.Calculate(billableWeight, segment, cell);

            // 获取该店铺当日单量（用于单量加收附加费）
            int dailyVolume = 0;
            if (!string.IsNullOrEmpty(waybill.ShopName))
            {
                dailyVolumeMap.TryGetValue(
                    (waybill.ShopName, waybill.WaybillDate.Date),
                    out dailyVolume);
            }

            // 附加费（三级作用域：报价级 > 业务对象级 > 全局）
            var surcharge = surchargeCache.CalcSurcharges(
                waybill.BrandCode, waybill.NetworkPointCode,
                clientType, quotation.ClientId ?? string.Empty,
                quotation.QuotationId,
                provinceId, billableWeight, waybill.WaybillDate, dailyVolume);

            // 保价费计算
            decimal insuranceFee = 0m;
            if (quotation.InsuranceRate.HasValue && quotation.InsuranceRate.Value > 0
                && waybill.DeclarationValue.HasValue && waybill.DeclarationValue.Value > 0)
            {
                insuranceFee = Math.Round(waybill.DeclarationValue.Value * quotation.InsuranceRate.Value, 2);
            }

            var chargeAmount = freight + surcharge + insuranceFee;

            // 生成计费结果
            billingResults.Add(new ExpBillingResult
            {
                FBatchId = 0, // 将在下方统一赋值
                FWaybillNo = waybill.WaybillNo,
                FWaybillDate = waybill.WaybillDate,
                FPartyClientId = quotation.ClientId ?? string.Empty,
                FPartyClientName = quotation.ClientName ?? string.Empty,
                FClientType = clientType,
                FPartyRole = 1,                  // 统一为应收
                FChainLevel = stepIndex,         // 复用为步骤序号 0-5
                FBrandCode = waybill.BrandCode,
                FBillingDate = DateTime.Today,
                FBillableWeight = billableWeight,
                FFreightCharge = freight,
                FInsuranceFee = insuranceFee > 0 ? insuranceFee : (decimal?)null,
                FSurchargeAmount = surcharge,
                FChargeAmount = chargeAmount,
                FQuotationId = quotation.QuotationId,
                FQuotationCode = quotation.PlanCode,
                FNetworkPointCode = waybill.NetworkPointCode,
                FDestinationProvinceId = provinceId,
                FDestProvinceName = waybill.DestinationProvince,
                FCalcStatus = 1,
                FOrgId = waybill.OrgId,
            });

            hasAnyResult = true;

            // 收集网点不一致信息（每个waybill只记录一次）
            if (!mismatchRecorded
                && waybill.NetworkPointCode != null
                && quotation.NetworkPointCode != null
                && waybill.NetworkPointCode != quotation.NetworkPointCode)
            {
                npMismatches.Add(new NetworkPointMismatchInfo
                {
                    WaybillNo = waybill.WaybillNo,
                    RowId = waybill.RowId,
                    WaybillNpCode = waybill.NetworkPointCode,
                    QuotationNpCode = quotation.NetworkPointCode
                });
                mismatchRecorded = true;
            }

            // 佣金计算（内嵌于报价）
            if (quotation.HasCommission)
            {
                var commissionAmount = quotation.CommissionCalcMethod switch
                {
                    1 => quotation.CommissionFixedAmount ?? 0,
                    2 => chargeAmount * (quotation.CommissionRate ?? 0),
                    3 => billableWeight * (quotation.CommissionWeightAmount ?? 0),
                    _ => 0m
                };
                if (commissionAmount != 0)
                {
                    billingResults.Add(new ExpBillingResult
                    {
                        FBatchId = 0, // 将在下方统一赋值
                        FWaybillNo = waybill.WaybillNo,
                        FWaybillDate = waybill.WaybillDate,
                        FPartyClientId = quotation.CommissionTargetId ?? string.Empty,
                        FPartyClientName = quotation.CommissionTargetName ?? string.Empty,
                        FClientType = quotation.CommissionTargetType,
                        FPartyRole = 3,   // 佣金
                        FChainLevel = stepIndex,
                        FBrandCode = waybill.BrandCode,
                        FBillingDate = DateTime.Today,
                        FBillableWeight = billableWeight,
                        FCommissionAmount = commissionAmount,
                        FChargeAmount = commissionAmount,
                        FQuotationId = quotation.QuotationId,
                        FNetworkPointCode = waybill.NetworkPointCode,
                        FDestinationProvinceId = provinceId,
                        FDestProvinceName = waybill.DestinationProvince,
                        FCalcStatus = 1,
                        FOrgId = waybill.OrgId,
                    });
                }
            }

            stepIndex++;
        }

        if (!hasAnyResult)
            throw new BillingException("ERR_NO_PRICE_PLAN", $"店铺 {waybill.ShopName} 无任何生效报价");
    }

    private static PricingEngineExplainResult ExplainSingleWaybill(
        BillingWaybillData waybill,
        QuotationLookupCache quotationCache,
        PricePlanCache priceCache,
        SurchargeCache surchargeCache,
        Dictionary<(string ShopName, DateTime Date), int> dailyVolumeMap)
    {
        var result = new PricingEngineExplainResult
        {
            RowId = waybill.RowId,
            WaybillNo = waybill.WaybillNo,
            ShopName = waybill.ShopName,
            BrandCode = waybill.BrandCode,
            DestinationProvinceId = waybill.DestinationProvinceId,
            DestinationProvince = waybill.DestinationProvince,
            DestinationCityName = waybill.DestinationCityName
        };

        var hasAnyResult = false;
        var stepIndex = 0;

        foreach (var clientType in ClientTypeSteps)
        {
            var step = new PricingClientTypeExplainResult
            {
                ClientType = clientType,
                StepIndex = stepIndex
            };
            result.ClientTypeResults.Add(step);

            var quotation = quotationCache.FindQuotation(
                waybill.ShopName,
                waybill.BrandCode,
                clientType,
                waybill.WaybillDate,
                alias: waybill.ClientAlias);

            if (quotation == null)
            {
                AddConfigurationIssue(result, step, $"未命中 {clientType} 报价方案");
                stepIndex++;
                continue;
            }

            step.QuotationMatched = true;
            step.QuotationId = quotation.QuotationId;
            step.QuotationCode = quotation.PlanCode;
            step.ClientId = quotation.ClientId;
            step.ClientName = quotation.ClientName;
            step.NetworkPointCode = quotation.NetworkPointCode;

            var billableWeight = GetBillingWeight(waybill, quotation.SettlementWeightStage);
            if (!billableWeight.HasValue)
            {
                AddConfigurationIssue(result, step, "无有效重量");
                result.ErrorCode = "ERR_NO_WEIGHT";
                result.ErrorMessage = "无有效重量";
                return result;
            }

            step.BillableWeight = billableWeight;

            var segment = priceCache.FindSegment(quotation.QuotationId, billableWeight.Value);
            if (segment == null)
            {
                AddConfigurationIssue(result, step, $"报价 {quotation.PlanCode} 未命中重量段");
                stepIndex++;
                continue;
            }

            step.SegmentMatched = true;
            step.SegmentIndex = segment.SegmentIndex;

            var provinceId = waybill.DestinationProvinceId ?? 0;
            var cell = priceCache.FindCell(quotation.QuotationId, segment.SegmentIndex, provinceId, waybill.DestinationCityName);
            if (cell == null)
            {
                AddConfigurationIssue(result, step, $"价格矩阵缺失: Quotation={quotation.PlanCode}, Type={clientType}");
                result.ErrorCode = "ERR_NO_PRICE_CELL";
                result.ErrorMessage = $"价格矩阵缺失: Quotation={quotation.PlanCode}, Type={clientType}";
                return result;
            }

            step.PriceCellMatched = true;
            step.ProvinceId = cell.ProvinceId;

            var formula = PriceFormula.Explain(billableWeight.Value, segment, cell);
            step.Formula = formula;
            step.Freight = formula.Amount;

            var dailyVolume = 0;
            if (!string.IsNullOrEmpty(waybill.ShopName))
            {
                dailyVolumeMap.TryGetValue((waybill.ShopName, waybill.WaybillDate.Date), out dailyVolume);
            }
            step.DailyVolume = dailyVolume;

            var surcharge = surchargeCache.CalcSurcharges(
                waybill.BrandCode,
                waybill.NetworkPointCode,
                clientType,
                quotation.ClientId ?? string.Empty,
                quotation.QuotationId,
                provinceId,
                billableWeight.Value,
                waybill.WaybillDate,
                dailyVolume);
            step.Surcharge = surcharge;

            var insuranceFee = 0m;
            if (quotation.InsuranceRate.HasValue && quotation.InsuranceRate.Value > 0
                && waybill.DeclarationValue.HasValue && waybill.DeclarationValue.Value > 0)
            {
                insuranceFee = Math.Round(waybill.DeclarationValue.Value * quotation.InsuranceRate.Value, 2);
            }
            step.InsuranceFee = insuranceFee;

            var chargeAmount = formula.Amount + surcharge + insuranceFee;
            step.ChargeAmount = chargeAmount;
            result.TotalChargeAmount += chargeAmount;
            result.SuccessResultCount++;
            hasAnyResult = true;

            if (quotation.HasCommission)
            {
                var commissionAmount = quotation.CommissionCalcMethod switch
                {
                    1 => quotation.CommissionFixedAmount ?? 0,
                    2 => chargeAmount * (quotation.CommissionRate ?? 0),
                    3 => billableWeight.Value * (quotation.CommissionWeightAmount ?? 0),
                    _ => 0m
                };

                if (commissionAmount != 0m)
                {
                    step.CommissionAmount = commissionAmount;
                    result.TotalChargeAmount += commissionAmount;
                    result.SuccessResultCount++;
                }
            }

            stepIndex++;
        }

        if (!hasAnyResult)
        {
            result.ErrorCode = "ERR_NO_PRICE_PLAN";
            result.ErrorMessage = $"店铺 {waybill.ShopName} 无任何生效报价";
            result.ConfigurationIssues.Add(result.ErrorMessage);
        }

        return result;
    }

    private static void AddConfigurationIssue(
        PricingEngineExplainResult result,
        PricingClientTypeExplainResult step,
        string issue)
    {
        step.ConfigurationIssues.Add(issue);
        result.ConfigurationIssues.Add(issue);
    }

    /// <summary>
    /// 计费重量取值：优先使用直填结算重量，其次按报价方案指定环节取值，最后取各环节最大值
    /// </summary>
    private static decimal? GetBillingWeight(BillingWaybillData waybill, int settlementWeightStage)
    {
        // 优先级1：如果有直填的结算重量，使用它
        if (waybill.SettlementWeight.HasValue && waybill.SettlementWeight > 0)
            return waybill.SettlementWeight;

        // 优先级2：根据报价方案指定的环节取值
        decimal? stageWeight = settlementWeightStage switch
        {
            1 => waybill.PickupWeight,      // 揽收重量
            2 => waybill.TransitWeight,     // 中转重量
            3 => waybill.DeliveryWeight,    // 到件重量
            4 => waybill.BundleWeight,      // 集包重量
            5 => waybill.VolumeWeight,      // 计泡重量
            6 => waybill.HqWeight,          // 总部重量
            _ => null                        // 7或其他：走最大值逻辑
        };
        if (stageWeight.HasValue && stageWeight > 0)
            return stageWeight;

        // 优先级3：取各环节重量的最大值（stage=7 或指定环节无值时兜底）
        var weights = new[]
        {
            waybill.PickupWeight,
            waybill.TransitWeight,
            waybill.DeliveryWeight,
            waybill.BundleWeight,
            waybill.VolumeWeight,
            waybill.HqWeight
        };

        decimal? maxWeight = null;
        foreach (var w in weights)
        {
            if (w.HasValue && w > 0 && (maxWeight == null || w > maxWeight))
                maxWeight = w;
        }

        return maxWeight;
    }

    /// <summary>
    /// 批量更新 STG 表的计算状态（临时表 + JOIN 原子更新）
    /// 使用 SqlBulkCopy 写入临时表后一次性 UPDATE，确保同一事务内原子完成。
    /// </summary>
    private static async Task BulkUpdateStgStatus(
        List<BillingWaybillData> waybills, string sourceTable,
        SqlConnection connection, SqlTransaction transaction)
    {
        if (waybills.Count == 0) return;

        // 按状态分组批量更新
        var groups = waybills.GroupBy(w => w.BillingStatus);
        foreach (var group in groups)
        {
            var ids = group.Select(w => w.RowId).ToList();
            var status = group.Key;

            await BulkUpdateStgStatusByTempTable(connection, transaction, sourceTable, ids, status);
        }
    }

    /// <summary>
    /// 使用临时表 + INNER JOIN 实现原子批量状态更新
    /// </summary>
    private static async Task BulkUpdateStgStatusByTempTable(
        SqlConnection conn, SqlTransaction tx,
        string stgTable, IReadOnlyList<long> ids, int status)
    {
        if (ids.Count == 0) return;

        // 1. 创建临时表（会话级，自动随连接关闭销毁）
        var createTempSql = "CREATE TABLE #TmpBillingIds (FID BIGINT NOT NULL PRIMARY KEY);";
        using (var cmd = new SqlCommand(createTempSql, conn, tx))
            await cmd.ExecuteNonQueryAsync();

        // 2. 使用 SqlBulkCopy 批量写入临时表
        using (var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, tx))
        {
            bulkCopy.DestinationTableName = "#TmpBillingIds";
            var dt = new DataTable();
            dt.Columns.Add("FID", typeof(long));
            foreach (var id in ids)
                dt.Rows.Add(id);
            await bulkCopy.WriteToServerAsync(dt);
        }

        // 3. 使用 UPDATE...INNER JOIN 一次性更新，然后清理临时表
        // 表名已通过 ValidateTableName 验证，安全拼接
        var updateSql = $@"
            UPDATE s SET s.[F计算状态] = @status
            FROM [{stgTable}] s
            INNER JOIN #TmpBillingIds t ON s.[FID] = t.[FID];
            DROP TABLE #TmpBillingIds;";
        using (var cmd = new SqlCommand(updateSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@status", status);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

public class PricingEngineExplainResult
{
    public long RowId { get; set; }
    public string WaybillNo { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string BrandCode { get; set; } = string.Empty;
    public int? DestinationProvinceId { get; set; }
    public string? DestinationProvince { get; set; }
    public string? DestinationCityName { get; set; }
    public bool Success => string.IsNullOrEmpty(ErrorCode) && SuccessResultCount > 0;
    public int SuccessResultCount { get; set; }
    public decimal TotalChargeAmount { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ConfigurationIssues { get; set; } = [];
    public List<PricingClientTypeExplainResult> ClientTypeResults { get; set; } = [];
}

public class PricingClientTypeExplainResult
{
    public string ClientType { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public bool QuotationMatched { get; set; }
    public long? QuotationId { get; set; }
    public string? QuotationCode { get; set; }
    public string? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string? NetworkPointCode { get; set; }
    public decimal? BillableWeight { get; set; }
    public bool SegmentMatched { get; set; }
    public int? SegmentIndex { get; set; }
    public bool PriceCellMatched { get; set; }
    public int? ProvinceId { get; set; }
    public int DailyVolume { get; set; }
    public decimal? Freight { get; set; }
    public decimal? Surcharge { get; set; }
    public decimal? InsuranceFee { get; set; }
    public decimal? CommissionAmount { get; set; }
    public decimal? ChargeAmount { get; set; }
    public PriceFormulaExplainResult? Formula { get; set; }
    public List<string> ConfigurationIssues { get; set; } = [];
}
