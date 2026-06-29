using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Validation;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Express.Services.Agents;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// 导入计算验证工作台的价格解释实现（IPricingExplainProvider 在 CardFlow 定义，此处实现以倒置依赖方向）。
/// 复用价格计算节点的插件规则配置（sourceTable/columnMapping/brandCode）读取 STG 行，
/// 走 PricingPlugin 同一套字段映射 + PricingEngine 只读解释，保证解释口径与计费一致。
/// 任何环节失败均返回空结果，不影响验证主流程。
/// </summary>
public class PricingExplainProvider : IPricingExplainProvider
{
    /// <summary>价格计算插件在 CF自动插件注册 中的编码</summary>
    private const string PricingPluginCode = "Pricing";

    private readonly STOTOPDbContext _db;
    private readonly PricingEngine _pricingEngine;
    private readonly ProvinceCache _provinceCache;
    private readonly IRepository<ExpProvince> _provinceRepo;
    private readonly IRepository<ExpNetworkPoint> _networkPointRepo;
    private readonly IRepository<ExpNetworkPointAlias> _networkPointAliasRepo;
    private readonly string _connectionString;
    private readonly ILogger<PricingExplainProvider> _logger;

    public PricingExplainProvider(
        STOTOPDbContext db,
        PricingEngine pricingEngine,
        ProvinceCache provinceCache,
        IRepository<ExpProvince> provinceRepo,
        IRepository<ExpNetworkPoint> networkPointRepo,
        IRepository<ExpNetworkPointAlias> networkPointAliasRepo,
        IConfiguration configuration,
        ILogger<PricingExplainProvider> logger)
    {
        _db = db;
        _pricingEngine = pricingEngine;
        _provinceCache = provinceCache;
        _provinceRepo = provinceRepo;
        _networkPointRepo = networkPointRepo;
        _networkPointAliasRepo = networkPointAliasRepo;
        _connectionString = DbConnectionsHelper.GetSystemConnectionString(configuration.GetValue<string>("Security:EncryptionKey"))
            ?? throw new InvalidOperationException("无法获取数据库连接字符串");
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, PricingExplainSnapshot>> ExplainAsync(
        PricingExplainRequest request,
        CancellationToken cancellationToken = default)
    {
        var empty = new Dictionary<string, PricingExplainSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (request.WaybillNos.Count == 0)
            return empty;

        try
        {
            var config = await ResolvePricingConfigAsync(request.FlowDefinitionId, cancellationToken);
            if (config == null)
                return empty;

            var waybills = await LoadWaybillsAsync(request, config, cancellationToken);
            if (waybills.Count == 0)
                return empty;

            foreach (var waybill in waybills)
                waybill.OrgId = request.OrgId;

            await _provinceCache.LoadAsync(_provinceRepo);
            foreach (var waybill in waybills)
            {
                if (!string.IsNullOrEmpty(waybill.DestinationProvince))
                    waybill.DestinationProvinceId = _provinceCache.FindId(waybill.DestinationProvince);
            }

            await ResolveNetworkPointCodesAsync(waybills, cancellationToken);

            var dailyVolumeMap = await LoadDailyVolumeMapAsync(request, config, waybills, cancellationToken);

            var results = await _pricingEngine.ExplainBatchAsync(waybills, dailyVolumeMap, cancellationToken);

            return results
                .Where(r => !string.IsNullOrWhiteSpace(r.WaybillNo))
                .GroupBy(r => r.WaybillNo, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => ToSnapshot(g.First()), StringComparer.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "价格解释失败（批次={BatchId}），验证将跳过解释对比", request.BatchId);
            return empty;
        }
    }

    private sealed class PricingNodeConfig
    {
        public string SourceTable { get; init; } = string.Empty;
        public string BrandCode { get; init; } = string.Empty;
        public Dictionary<string, string> ColumnMapping { get; init; } = new();
    }

    /// <summary>
    /// 按流程定义找到价格计算节点的插件规则配置。
    /// 取当前版本的节点链；批次若由历史版本执行，解释按当前配置进行（验证台定位的是“现在该怎么算”）。
    /// </summary>
    private async Task<PricingNodeConfig?> ResolvePricingConfigAsync(long flowDefinitionId, CancellationToken cancellationToken)
    {
        var registryIds = await _db.Set<CfAutoPluginRegistry>()
            .AsNoTracking()
            .Where(r => r.F插件编码 == PricingPluginCode)
            .Select(r => r.FID)
            .ToListAsync(cancellationToken);

        if (registryIds.Count == 0)
            return null;

        var versionId = await _db.Set<CfFlowVersion>()
            .AsNoTracking()
            .Where(v => v.FFlowDefinitionId == flowDefinitionId && v.FIsCurrentVersion)
            .OrderByDescending(v => v.FVersionNumber)
            .Select(v => (long?)v.FID)
            .FirstOrDefaultAsync(cancellationToken);

        if (versionId == null)
            return null;

        var ruleId = await _db.Set<CfStageDefinition>()
            .AsNoTracking()
            .Where(s => s.FFlowVersionId == versionId.Value
                && s.F插件注册ID != null
                && registryIds.Contains(s.F插件注册ID.Value)
                && s.F插件规则ID != null)
            .OrderBy(s => s.FSortOrder)
            .Select(s => s.F插件规则ID)
            .FirstOrDefaultAsync(cancellationToken);

        if (ruleId == null)
            return null;

        var ruleJson = await _db.Set<CfPluginRule>()
            .AsNoTracking()
            .Where(r => r.FID == ruleId.Value)
            .Select(r => r.F规则配置JSON)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(ruleJson))
            return null;

        using var doc = JsonDocument.Parse(ruleJson);
        var root = doc.RootElement;

        var sourceTable = root.TryGetProperty("sourceTable", out var tableProp) && tableProp.ValueKind == JsonValueKind.String
            ? tableProp.GetString()
            : null;
        var brandCode = root.TryGetProperty("brandCode", out var brandProp) && brandProp.ValueKind == JsonValueKind.String
            ? brandProp.GetString() ?? string.Empty
            : string.Empty;
        var columnMapping = root.TryGetProperty("columnMapping", out var mappingProp)
            ? PricingPlugin.ParseColumnMapping(mappingProp)
            : new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(sourceTable)
            || string.IsNullOrWhiteSpace(brandCode)
            || !columnMapping.ContainsKey("waybillNo"))
            return null;

        PricingPlugin.ValidateTableName(sourceTable);

        return new PricingNodeConfig
        {
            SourceTable = sourceTable,
            BrandCode = brandCode,
            ColumnMapping = columnMapping
        };
    }

    private async Task<List<BillingWaybillData>> LoadWaybillsAsync(
        PricingExplainRequest request,
        PricingNodeConfig config,
        CancellationToken cancellationToken)
    {
        var waybillColumn = config.ColumnMapping["waybillNo"];

        var columns = new List<string> { "[FID]" };
        foreach (var column in config.ColumnMapping.Values)
        {
            if (!columns.Contains($"[{column}]"))
                columns.Add($"[{column}]");
        }

        var rows = new List<Dictionary<string, object>>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var chunk in request.WaybillNos
                     .Where(no => !string.IsNullOrWhiteSpace(no))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Chunk(500))
        {
            var parameterNames = chunk.Select((_, index) => $"@no{index}").ToArray();
            var sql = $"""
                SELECT {string.Join(", ", columns)}
                FROM [{config.SourceTable}]
                WHERE [F批次ID] = @batchId
                  AND [FOrgId] = @orgId
                  AND [{waybillColumn}] IN ({string.Join(", ", parameterNames)})
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@batchId", request.BatchId);
            command.Parameters.AddWithValue("@orgId", request.OrgId);
            for (var i = 0; i < chunk.Length; i++)
                command.Parameters.AddWithValue(parameterNames[i], chunk[i]);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var dict = new Dictionary<string, object>();
                for (var i = 0; i < reader.FieldCount; i++)
                    dict[reader.GetName(i)] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                rows.Add(dict);
            }
        }

        return PricingPlugin.MapToBillingData(rows, config.BrandCode, config.ColumnMapping);
    }

    /// <summary>网点名称 → 编码（全称/简称优先，别名表兜底），与 PricingPlugin 预检口径一致。</summary>
    private async Task ResolveNetworkPointCodesAsync(List<BillingWaybillData> waybills, CancellationToken cancellationToken)
    {
        if (!waybills.Any(w => !string.IsNullOrWhiteSpace(w.NetworkPointName)))
            return;

        var networkPoints = await _networkPointRepo.Query()
            .Select(np => new { np.FCode, np.FFullName, np.FShortName })
            .ToListAsync(cancellationToken);

        var aliasMap = new Dictionary<string, string>();
        foreach (var np in networkPoints)
        {
            if (!string.IsNullOrEmpty(np.FFullName))
                aliasMap[np.FFullName] = np.FCode;
            if (!string.IsNullOrEmpty(np.FShortName) && !aliasMap.ContainsKey(np.FShortName))
                aliasMap[np.FShortName] = np.FCode;
        }

        var aliases = await _networkPointAliasRepo.Query().ToListAsync(cancellationToken);
        foreach (var alias in aliases)
        {
            if (!string.IsNullOrEmpty(alias.FName) && !aliasMap.ContainsKey(alias.FName))
                aliasMap[alias.FName] = alias.FNetworkPointCode;
        }

        foreach (var waybill in waybills)
        {
            if (!string.IsNullOrWhiteSpace(waybill.NetworkPointName)
                && aliasMap.TryGetValue(waybill.NetworkPointName.Trim(), out var code))
                waybill.NetworkPointCode = code;
        }
    }

    /// <summary>
    /// 按整批 STG 数据统计抽样店铺的单日票量（票量阶梯附加费用）。
    /// 注：计费运行时按“当次送入引擎的运单”统计，二者对被预检排除的行存在少量口径差，解释结果标注公式即可定位。
    /// </summary>
    private async Task<Dictionary<(string ShopName, DateTime Date), int>?> LoadDailyVolumeMapAsync(
        PricingExplainRequest request,
        PricingNodeConfig config,
        List<BillingWaybillData> waybills,
        CancellationToken cancellationToken)
    {
        if (!config.ColumnMapping.TryGetValue("shopName", out var shopColumn)
            || !config.ColumnMapping.TryGetValue("waybillDate", out var dateColumn))
            return null;

        var shopNames = waybills
            .Select(w => w.ShopName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(500)
            .ToArray();

        if (shopNames.Length == 0)
            return null;

        var parameterNames = shopNames.Select((_, index) => $"@shop{index}").ToArray();
        var sql = $"""
            SELECT [{shopColumn}] AS ShopName, CONVERT(date, [{dateColumn}]) AS WaybillDate, COUNT(1) AS Volume
            FROM [{config.SourceTable}]
            WHERE [F批次ID] = @batchId
              AND [FOrgId] = @orgId
              AND [{shopColumn}] IN ({string.Join(", ", parameterNames)})
            GROUP BY [{shopColumn}], CONVERT(date, [{dateColumn}])
            """;

        var map = new Dictionary<(string ShopName, DateTime Date), int>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@batchId", request.BatchId);
        command.Parameters.AddWithValue("@orgId", request.OrgId);
        for (var i = 0; i < shopNames.Length; i++)
            command.Parameters.AddWithValue(parameterNames[i], shopNames[i]);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(0) || reader.IsDBNull(1))
                continue;
            var shopName = reader.GetString(0);
            var date = reader.GetDateTime(1).Date;
            map[(shopName, date)] = reader.GetInt32(2);
        }

        return map.Count > 0 ? map : null;
    }

    private static PricingExplainSnapshot ToSnapshot(PricingEngineExplainResult result)
    {
        return new PricingExplainSnapshot
        {
            WaybillNo = result.WaybillNo,
            Success = result.Success,
            ErrorCode = result.ErrorCode,
            ErrorMessage = result.ErrorMessage,
            TotalChargeAmount = result.TotalChargeAmount,
            ConfigurationIssues = result.ConfigurationIssues.ToList(),
            Steps = result.ClientTypeResults
                .Select(step => new PricingExplainStepSnapshot
                {
                    ClientType = step.ClientType,
                    QuotationMatched = step.QuotationMatched,
                    QuotationCode = step.QuotationCode,
                    ClientName = step.ClientName,
                    BillableWeight = step.BillableWeight,
                    SegmentMatched = step.SegmentMatched,
                    SegmentIndex = step.SegmentIndex,
                    PriceCellMatched = step.PriceCellMatched,
                    Freight = step.Freight,
                    Surcharge = step.Surcharge,
                    InsuranceFee = step.InsuranceFee,
                    CommissionAmount = step.CommissionAmount,
                    ChargeAmount = step.ChargeAmount,
                    FormulaText = FormatFormula(step.Formula),
                    ConfigurationIssues = step.ConfigurationIssues.ToList()
                })
                .ToList()
        };
    }

    private static string? FormatFormula(PriceFormulaExplainResult? formula)
    {
        if (formula == null)
            return null;

        return $"{formula.Formula} | 首重={formula.FirstWeight}, 首重价={formula.BasePrice}, "
            + $"续重步长={formula.ContinueStep}, 续重价={formula.ContinuePrice}, "
            + $"进位后重量={formula.RoundedWeight}, 金额={formula.Amount}";
    }
}
