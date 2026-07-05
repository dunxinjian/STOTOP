using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// SqlBulkCopy 批量写入
/// </summary>
public class BillingBulkWriter
{
    private readonly string _connectionString;
    private readonly IConfiguration _configuration;

    public BillingBulkWriter(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = DbConnectionsHelper.GetSystemConnectionString(_configuration.GetValue<string>("Security:EncryptionKey"))
            ?? throw new InvalidOperationException("无法获取数据库连接字符串");
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
    /// 批量写入计费结果，返回写入后的 (WaybillId → BillingResultId) 映射
    /// </summary>
    public async Task<Dictionary<long, long>> BulkInsertBillingResults(List<ExpBillingResult> results, string resultTable, SqlConnection? externalConn = null, SqlTransaction? externalTxn = null)
    {
        if (results.Count == 0) return new();
        ValidateTableName(resultTable);

        var table = new DataTable();
        table.Columns.Add("F批次ID", typeof(long));
        table.Columns.Add("F运单编号", typeof(string));
        table.Columns.Add("F运单日期", typeof(DateTime));
        table.Columns.Add("F业务对象编号", typeof(string));
        table.Columns.Add("F业务对象名称", typeof(string));
        table.Columns.Add("F参与方角色", typeof(int));
        table.Columns.Add("F层级", typeof(int));
        table.Columns.Add("F品牌编码", typeof(string));
        table.Columns.Add("F计费日期", typeof(DateTime));
        table.Columns.Add("F计费重量", typeof(decimal));
        table.Columns.Add("F基础运费", typeof(decimal));
        table.Columns.Add("F保价费", typeof(decimal));
        table.Columns.Add("F加收费用", typeof(decimal));
        table.Columns.Add("F减免金额", typeof(decimal));
        table.Columns.Add("F佣金金额", typeof(decimal));
        table.Columns.Add("F应收金额", typeof(decimal));
        table.Columns.Add("F报价ID", typeof(long));
        table.Columns.Add("F佣金规则ID", typeof(long));
        table.Columns.Add("F业务对象类型", typeof(string));
        table.Columns.Add("F报价编号", typeof(string));
        table.Columns.Add("F计算状态", typeof(int));
        table.Columns.Add("F异常信息", typeof(string));
        table.Columns.Add("F账单ID", typeof(long));
        table.Columns.Add("F目的省份ID", typeof(int));
        table.Columns.Add("F目的省份", typeof(string));
        table.Columns.Add("F归属网点编号", typeof(string));
        table.Columns.Add("F成本合计", typeof(decimal));
        table.Columns.Add("F组织ID", typeof(long));
        table.Columns.Add("F租户ID", typeof(long));

        foreach (var r in results)
        {
            var row = table.NewRow();
            row["F批次ID"] = r.FBatchId;
            row["F运单编号"] = (object?)r.FWaybillNo ?? DBNull.Value;
            row["F运单日期"] = (object?)r.FWaybillDate ?? DBNull.Value;
            row["F业务对象编号"] = r.FPartyClientId;
            row["F业务对象名称"] = (object?)r.FPartyClientName ?? DBNull.Value;
            row["F参与方角色"] = (object?)r.FPartyRole ?? DBNull.Value;
            row["F层级"] = (object?)r.FChainLevel ?? DBNull.Value;
            row["F品牌编码"] = (object?)r.FBrandCode ?? DBNull.Value;
            row["F计费日期"] = (object?)r.FBillingDate ?? DBNull.Value;
            row["F计费重量"] = (object?)r.FBillableWeight ?? DBNull.Value;
            row["F基础运费"] = (object?)r.FFreightCharge ?? DBNull.Value;
            row["F保价费"] = (object?)r.FInsuranceFee ?? DBNull.Value;
            row["F加收费用"] = (object?)r.FSurchargeAmount ?? DBNull.Value;
            row["F减免金额"] = (object?)r.FWaiverAmount ?? DBNull.Value;
            row["F佣金金额"] = (object?)r.FCommissionAmount ?? DBNull.Value;
            row["F应收金额"] = (object?)r.FChargeAmount ?? DBNull.Value;
            row["F报价ID"] = (object?)r.FQuotationId ?? DBNull.Value;
            row["F佣金规则ID"] = (object?)r.FCommissionRuleId ?? DBNull.Value;
            row["F业务对象类型"] = (object?)r.FClientType ?? DBNull.Value;
            row["F报价编号"] = (object?)r.FQuotationCode ?? DBNull.Value;
            row["F计算状态"] = r.FCalcStatus;
            row["F异常信息"] = (object?)r.FErrorMessage ?? DBNull.Value;
            row["F账单ID"] = (object?)r.FInvoiceId ?? DBNull.Value;
            row["F目的省份ID"] = r.FDestinationProvinceId;
            row["F目的省份"] = (object?)r.FDestProvinceName ?? DBNull.Value;
            row["F归属网点编号"] = (object?)r.FNetworkPointCode ?? DBNull.Value;
            row["F成本合计"] = r.FTotalCost;
            row["F组织ID"] = r.FOrgId;
            row["F租户ID"] = r.FTenantId;
            table.Rows.Add(row);
        }

        var ownsConnection = externalConn == null;
        var connection = externalConn ?? new SqlConnection(_connectionString);
        if (ownsConnection) await connection.OpenAsync();

        try
        {
            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, externalTxn)
            {
                DestinationTableName = $"[{resultTable}]",
                BatchSize = 5000
            };

        foreach (DataColumn col in table.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(table);

        // 方案A：查回 FID，按运单编号和参与方角色=1（Level 0）匹配
        var waybillToResultId = new Dictionary<long, long>();
        var waybillNos = results.Where(r => r.FPartyRole == 1 && r.FCalcStatus == 1)
            .Select(r => r.FWaybillNo).Where(n => n != null).Distinct().ToList();

        if (waybillNos.Count > 0)
        {
            // 分批查询
            const int batchSize = 1000;
            for (int i = 0; i < waybillNos.Count; i += batchSize)
            {
                var batch = waybillNos.Skip(i).Take(batchSize).ToList();
                var paramList = string.Join(",", batch.Select((_, idx) => $"@p{idx}"));
                var sql = $"SELECT FID, [F运单编号] FROM [{resultTable}] WHERE [F运单编号] IN ({paramList}) AND [F参与方角色] = 1 AND [F计算状态] = 1";
                using var cmd = new SqlCommand(sql, connection, externalTxn);
                for (int idx = 0; idx < batch.Count; idx++)
                    cmd.Parameters.AddWithValue($"@p{idx}", batch[idx]!);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var fid = reader.GetInt64(0);
                    var waybillNo = reader.GetString(1);
                    // 找到对应的原始 result 的 RowId——返回空字典即可，上层不再使用
                }
            }
        }

        return waybillToResultId;
        }
        finally
        {
            if (ownsConnection) await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// 批量写入成本明细
    /// </summary>
    public async Task BulkInsertCostBreakdowns(List<ExpBillingCostBreakdown> breakdowns, string costTable, SqlConnection? externalConn = null, SqlTransaction? externalTxn = null)
    {
        if (breakdowns.Count == 0) return;
        ValidateTableName(costTable);

        var table = new DataTable();
        table.Columns.Add("F计费结果ID", typeof(long));
        table.Columns.Add("F成本项目ID", typeof(int));
        table.Columns.Add("F金额", typeof(decimal));
        table.Columns.Add("F组织ID", typeof(long));
        table.Columns.Add("F租户ID", typeof(long));

        foreach (var b in breakdowns)
        {
            var row = table.NewRow();
            row["F计费结果ID"] = b.FBillingResultId;
            row["F成本项目ID"] = b.FCostItemId;
            row["F金额"] = b.FAmount;
            row["F组织ID"] = b.FOrgId;
            row["F租户ID"] = b.FTenantId;
            table.Rows.Add(row);
        }

        var ownsConnection = externalConn == null;
        var connection = externalConn ?? new SqlConnection(_connectionString);
        if (ownsConnection) await connection.OpenAsync();

        try
        {
            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, externalTxn)
            {
                DestinationTableName = $"[{costTable}]",
                BatchSize = 5000
            };

            foreach (DataColumn col in table.Columns)
            {
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            }

            await bulkCopy.WriteToServerAsync(table);
        }
        finally
        {
            if (ownsConnection) await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// 批量更新运单状态（分批 UPDATE）
    /// </summary>
    public async Task BulkUpdateWaybillStatus(List<ExpWaybill> waybills, SqlConnection? externalConn = null, SqlTransaction? externalTxn = null)
    {
        if (waybills.Count == 0) return;

        var ownsConnection = externalConn == null;
        var connection = externalConn ?? new SqlConnection(_connectionString);
        if (ownsConnection) await connection.OpenAsync();

        try
        {
            // 创建临时表
            var createTempSql = @"
            CREATE TABLE #TempWaybillUpdate (
                FID BIGINT PRIMARY KEY,
                F计费状态 INT,
                F业务对象ID NVARCHAR(50) NULL,
                F结算实重 DECIMAL(10,3) NULL,
                F计费重量 DECIMAL(10,3) NULL,
                F归属网点ID BIGINT NULL
            )";
        using (var cmd = new SqlCommand(createTempSql, connection, externalTxn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // 用 SqlBulkCopy 写入临时表
        var table = new DataTable();
        table.Columns.Add("FID", typeof(long));
        table.Columns.Add("F计费状态", typeof(int));
        table.Columns.Add("F业务对象ID", typeof(string));
        table.Columns.Add("F结算实重", typeof(decimal));
        table.Columns.Add("F计费重量", typeof(decimal));
        table.Columns.Add("F归属网点ID", typeof(long));

        foreach (var w in waybills)
        {
            var row = table.NewRow();
            row["FID"] = w.FID;
            row["F计费状态"] = w.FBillingStatus;
            row["F业务对象ID"] = (object?)w.FBusinessObjectId ?? DBNull.Value;
            row["F结算实重"] = (object?)w.FActualWeight ?? DBNull.Value;
            row["F计费重量"] = (object?)w.FBillableWeight ?? DBNull.Value;
            row["F归属网点ID"] = (object?)w.FNetworkPointId ?? DBNull.Value;
            table.Rows.Add(row);
        }

        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, externalTxn))
        {
            bulkCopy.DestinationTableName = "#TempWaybillUpdate";
            foreach (DataColumn col in table.Columns)
            {
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            }
            await bulkCopy.WriteToServerAsync(table);
        }

        // MERGE 更新
        var mergeSql = @"
            UPDATE w SET
                w.[F计费状态] = t.[F计费状态],
                w.[F业务对象ID] = t.[F业务对象ID],
                w.[F结算实重] = t.[F结算实重],
                w.[F计费重量] = t.[F计费重量],
                w.[F归属网点ID] = t.[F归属网点ID]
            FROM [EXP出港运单] w
            INNER JOIN #TempWaybillUpdate t ON w.FID = t.FID;
            DROP TABLE #TempWaybillUpdate;";
        using (var cmd = new SqlCommand(mergeSql, connection, externalTxn))
        {
            await cmd.ExecuteNonQueryAsync();
        }
        }
        finally
        {
            if (ownsConnection) await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// 删除指定运单编号的已有计费结果（用于重跑前清理，防止重复累积）
    /// 采用临时表+JOIN高性能模式。
    /// WHERE 限定 [F批次ID]=@batchId 防止跨批次误删（运单号跨批次可重复）；
    /// calcStatus 非空时再限定 [F计算状态]，供 Phase B 只删失败行、
    /// 不误删 Phase A 刚写入的同批次成功行。
    /// 删除范围的 C# 等价判定见 <see cref="BillingDeleteScope.WouldDeleteRow"/>。
    /// </summary>
    public async Task DeleteExistingResults(
        IReadOnlyList<string> waybillNos,
        string resultTable,
        long batchId,
        SqlConnection connection,
        SqlTransaction transaction,
        int? calcStatus = null)
    {
        if (waybillNos.Count == 0) return;
        ValidateTableName(resultTable);
        var costTable = $"{resultTable}_成本明细";
        ValidateTableName(costTable);

        // 1. 创建临时表
        var createTempSql = "CREATE TABLE #TmpDeleteWaybillNos ([FWaybillNo] NVARCHAR(50) NOT NULL);";
        using (var cmd = new SqlCommand(createTempSql, connection, transaction))
            await cmd.ExecuteNonQueryAsync();

        // 2. SqlBulkCopy 批量写入运单编号到临时表
        var dt = new DataTable();
        dt.Columns.Add("FWaybillNo", typeof(string));
        foreach (var no in waybillNos)
            dt.Rows.Add(no);

        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
        {
            bulkCopy.DestinationTableName = "#TmpDeleteWaybillNos";
            bulkCopy.BatchSize = 5000;
            await bulkCopy.WriteToServerAsync(dt);
        }

        // 3. 先删除成本明细，再删除旧结果，避免留下孤儿成本明细。
        //    WHERE 限定本批次（@batchId）；calcStatus 非空时追加状态限定。
        var statusClause = calcStatus.HasValue ? " AND r.[F计算状态] = @calcStatus" : "";
        var deleteSql = $@"
            IF OBJECT_ID(N'{costTable}', N'U') IS NOT NULL
            BEGIN
                DELETE c FROM [{costTable}] c
                INNER JOIN [{resultTable}] r ON c.[F计费结果ID] = r.[FID]
                INNER JOIN #TmpDeleteWaybillNos t ON r.[F运单编号] = t.[FWaybillNo]
                WHERE r.[F批次ID] = @batchId{statusClause};
            END;

            DELETE r FROM [{resultTable}] r
            INNER JOIN #TmpDeleteWaybillNos t ON r.[F运单编号] = t.[FWaybillNo]
            WHERE r.[F批次ID] = @batchId{statusClause};

            DROP TABLE #TmpDeleteWaybillNos;";
        using (var cmd = new SqlCommand(deleteSql, connection, transaction))
        {
            cmd.CommandTimeout = 120;
            cmd.Parameters.AddWithValue("@batchId", batchId);
            if (calcStatus.HasValue)
                cmd.Parameters.AddWithValue("@calcStatus", calcStatus.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// 获取连接字符串（供外部创建共享连接用）
    /// </summary>
    public string GetConnectionString() => _connectionString;
}
