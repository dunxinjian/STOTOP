using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.Express.Services;

public class WaybillArchiveService : IWaybillArchiveService
{
    private readonly string _connectionString;
    private const int BatchSize = 10000;
    private const int ArchiveAfterDays = 90;
    private readonly IConfiguration _configuration;

    public WaybillArchiveService(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = DbConnectionsHelper.GetSystemConnectionString(_configuration.GetValue<string>("Security:EncryptionKey"))
            ?? throw new InvalidOperationException("无法获取数据库连接字符串");
    }

    public async Task<ArchiveResultDto> ExecuteArchiveAsync()
    {
        var sw = Stopwatch.StartNew();
        long totalWaybills = 0, totalBillingResults = 0, totalCostBreakdowns = 0;

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        while (true)
        {
            // 查找符合归档条件的运单ID（账单已归档且归档时间距今超过90天）
            var findSql = $@"
                SELECT TOP ({BatchSize}) w.FID
                FROM [EXP出港运单] w
                INNER JOIN [EXP出港运单_计费结果] br ON br.[F运单编号] = w.[F运单编号]
                INNER JOIN [EXP出港账单] inv ON inv.FID = br.[F账单ID]
                WHERE inv.[F已归档] = 1
                  AND inv.[F归档时间] <= DATEADD(DAY, -{ArchiveAfterDays}, GETDATE())
                GROUP BY w.FID";

            var waybillIds = new List<long>();
            using (var cmd = new SqlCommand(findSql, connection))
            {
                cmd.CommandTimeout = 120;
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    waybillIds.Add(reader.GetInt64(0));
                }
            }

            if (waybillIds.Count == 0) break;

            var idList = string.Join(",", waybillIds);

            // 在事务内执行归档
            using var transaction = connection.BeginTransaction();
            try
            {
                // 1. INSERT INTO 运单历史
                var archiveWaybillSql = $@"
                    INSERT INTO [EXP出港运单_历史]
                        (FID, [F运单编号], [F品牌编码], [F店铺名称], [F业务对象ID], [F寄件省], [F目的省份ID],
                         [F揽收重量], [F中转重量], [F到件重量], [F集包重量], [F计泡重量], [F总部重量],
                         [F一单到底], [F结算实重], [F长], [F宽], [F高],
                         [F计费重量], [F声明价值], [F运单日期], [F导入批次ID], [F客户别名],
                         [F计费状态], [F创建时间], [F归档时间])
                    SELECT FID, [F运单编号], [F品牌编码], [F店铺名称], [F业务对象ID], [F寄件省], [F目的省份ID],
                           [F揽收重量], [F中转重量], [F到件重量], [F集包重量], [F计泡重量], [F总部重量],
                           [F一单到底], [F结算实重], [F长], [F宽], [F高],
                           [F计费重量], [F声明价值], [F运单日期], [F导入批次ID], [F客户别名],
                           [F计费状态], [F创建时间], GETDATE()
                    FROM [EXP出港运单]
                    WHERE FID IN ({idList})";

                int waybillCount;
                using (var cmd = new SqlCommand(archiveWaybillSql, connection, transaction))
                {
                    cmd.CommandTimeout = 300;
                    waybillCount = await cmd.ExecuteNonQueryAsync();
                }

                // 2. INSERT INTO 计费结果历史
                var archiveBillingSql = $@"
                    INSERT INTO [EXP出港运单_计费结果_历史]
                        (FID, [F批次ID], [F运单编号], [F运单日期], [F业务对象编号], [F参与方角色], [F层级], [F品牌编码],
                         [F计费日期], [F计费重量], [F基础运费], [F保价费], [F加收费用], [F减免金额],
                         [F佣金金额], [F应收金额], [F业务对象类型], [F报价ID], [F报价编号], [F佣金规则ID], [F计算状态],
                         [F异常信息], [F账单ID], [F目的省份ID], [F目的省份], [F归属网点编号], [F成本合计], [F组织ID], [F归档时间])
                    SELECT FID, [F批次ID], [F运单编号], [F运单日期], [F业务对象编号], [F参与方角色], [F层级], [F品牌编码],
                           [F计费日期], [F计费重量], [F基础运费], [F保价费], [F加收费用], [F减免金额],
                           [F佣金金额], [F应收金额], [F业务对象类型], [F报价ID], [F报价编号], [F佣金规则ID], [F计算状态],
                           [F异常信息], [F账单ID], [F目的省份ID], [F目的省份], [F归属网点编号], [F成本合计], [F组织ID], GETDATE()
                    FROM [EXP出港运单_计费结果]
                    WHERE [F运单编号] IN (SELECT [F运单编号] FROM [EXP出港运单] WHERE FID IN ({idList}))";

                int billingCount;
                using (var cmd = new SqlCommand(archiveBillingSql, connection, transaction))
                {
                    cmd.CommandTimeout = 300;
                    billingCount = await cmd.ExecuteNonQueryAsync();
                }

                // 3. INSERT INTO 成本明细历史
                var archiveCostSql = $@"
                    INSERT INTO [EXP出港运单_计费结果_成本明细_历史]
                        (FID, [F计费结果ID], [F成本项目ID], [F金额], [F归档时间])
                    SELECT cbd.FID, cbd.[F计费结果ID], cbd.[F成本项目ID], cbd.[F金额], GETDATE()
                    FROM [EXP出港运单_计费结果_成本明细] cbd
                    INNER JOIN [EXP出港运单_计费结果] br ON br.FID = cbd.[F计费结果ID]
                    WHERE br.[F运单编号] IN (SELECT [F运单编号] FROM [EXP出港运单] WHERE FID IN ({idList}))";

                int costCount;
                using (var cmd = new SqlCommand(archiveCostSql, connection, transaction))
                {
                    cmd.CommandTimeout = 300;
                    costCount = await cmd.ExecuteNonQueryAsync();
                }

                // 4. DELETE 成本明细
                var deleteCostSql = $@"
                    DELETE cbd FROM [EXP出港运单_计费结果_成本明细] cbd
                    INNER JOIN [EXP出港运单_计费结果] br ON br.FID = cbd.[F计费结果ID]
                    WHERE br.[F运单编号] IN (SELECT [F运单编号] FROM [EXP出港运单] WHERE FID IN ({idList}))";
                using (var cmd = new SqlCommand(deleteCostSql, connection, transaction))
                {
                    cmd.CommandTimeout = 300;
                    await cmd.ExecuteNonQueryAsync();
                }

                // 5. DELETE 计费结果
                var deleteBillingSql = $"DELETE FROM [EXP出港运单_计费结果] WHERE [F运单编号] IN (SELECT [F运单编号] FROM [EXP出港运单] WHERE FID IN ({idList}))";
                using (var cmd = new SqlCommand(deleteBillingSql, connection, transaction))
                {
                    cmd.CommandTimeout = 300;
                    await cmd.ExecuteNonQueryAsync();
                }

                // 6. DELETE 运单
                var deleteWaybillSql = $"DELETE FROM [EXP出港运单] WHERE FID IN ({idList})";
                using (var cmd = new SqlCommand(deleteWaybillSql, connection, transaction))
                {
                    cmd.CommandTimeout = 300;
                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();

                totalWaybills += waybillCount;
                totalBillingResults += billingCount;
                totalCostBreakdowns += costCount;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        sw.Stop();
        return new ArchiveResultDto
        {
            WaybillCount = totalWaybills,
            BillingResultCount = totalBillingResults,
            CostBreakdownCount = totalCostBreakdowns,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }

    public async Task<ArchiveStatsDto> GetArchiveStatsAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // 待归档数量
        var pendingSql = $@"
            SELECT COUNT(DISTINCT w.FID)
            FROM [EXP出港运单] w
            INNER JOIN [EXP出港运单_计费结果] br ON br.[F运单编号] = w.[F运单编号]
            INNER JOIN [EXP出港账单] inv ON inv.FID = br.[F账单ID]
            WHERE inv.[F已归档] = 1
              AND inv.[F归档时间] <= DATEADD(DAY, -{ArchiveAfterDays}, GETDATE())";

        long pendingCount;
        using (var cmd = new SqlCommand(pendingSql, connection))
        {
            cmd.CommandTimeout = 60;
            pendingCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        // 已归档数量
        var archivedSql = "SELECT COUNT(*) FROM [EXP出港运单_历史]";
        long archivedCount;
        using (var cmd = new SqlCommand(archivedSql, connection))
        {
            cmd.CommandTimeout = 60;
            archivedCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        return new ArchiveStatsDto
        {
            PendingCount = pendingCount,
            ArchivedCount = archivedCount
        };
    }
}
