using System.Text.RegularExpressions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Workflow.Entities;
using STOTOP.Module.Workflow.Enums;

namespace STOTOP.Module.CardFlow.Jobs;

/// <summary>
/// 孤立数据定期监控 Job：
/// 检测因批次物理删除后残留的孤立数据（工作项、凭证、暂存行），
/// 对孤立工作项自动标记取消，对凭证和暂存行仅告警。
/// 每日凌晨3点执行。
/// </summary>
[AutomaticRetry(Attempts = 3)]
public class OrphanedDataMonitorJob
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<OrphanedDataMonitorJob> _logger;

    public OrphanedDataMonitorJob(STOTOPDbContext context, ILogger<OrphanedDataMonitorJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("开始执行孤立数据监控检查...");
        try
        {
            await DetectOrphanedWorkItemsAsync();
            await DetectOrphanedVouchersAsync();
            await DetectOrphanedStagingRowsAsync();
            _logger.LogInformation("孤立数据监控检查完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "孤立数据监控 Job 执行失败");
            throw; // 让 Hangfire 自动重试
        }
    }

    /// <summary>
    /// 检测孤立工作项：批次已物理删除且创建超过24小时的 Pending/InProgress 工作项，自动标记 Cancelled
    /// </summary>
    private async Task DetectOrphanedWorkItemsAsync()
    {
        var sql = @"
            UPDATE w SET 
                w.[F状态] = @p0, 
                w.[F备注] = N'孤立数据自动清理：关联批次已不存在', 
                w.[F更新时间] = GETDATE()
            FROM [WF工作项] w
            WHERE w.[F模块] = N'DataCenter' 
              AND w.[F业务类型] = N'ImportBatch'
              AND w.[F创建时间] < DATEADD(HOUR, -24, GETDATE())
              AND w.[F状态] IN (@p1, @p2)
              AND NOT EXISTS (
                  SELECT 1 FROM [CF批次] b WHERE b.FID = w.[F业务ID]
              );";

        var cancelledStatus = (int)WorkItemStatus.Cancelled;
        var pendingStatus = (int)WorkItemStatus.Pending;
        var inProgressStatus = (int)WorkItemStatus.InProgress;

        var affected = await _context.Database.ExecuteSqlRawAsync(
            sql, cancelledStatus, pendingStatus, inProgressStatus);

        if (affected > 0)
        {
            _logger.LogWarning("发现并自动取消 {Count} 条孤立工作项（关联批次已不存在）", affected);
        }
        else
        {
            _logger.LogInformation("未发现孤立工作项");
        }
    }

    /// <summary>
    /// 检测孤立凭证：DataScopeId 不为空、创建超过24小时、且关联批次已不存在的凭证（仅告警）
    /// </summary>
    private async Task DetectOrphanedVouchersAsync()
    {
        var sql = @"
            SELECT COUNT(1) FROM [FIN凭证] v
            WHERE v.[F数据作用域ID] IS NOT NULL AND LEN(v.[F数据作用域ID]) > 0
              AND v.[F创建时间] < DATEADD(HOUR, -24, GETDATE())
              AND v.[F已撤销] = 0
              AND NOT EXISTS (
                  SELECT 1 FROM [CF批次] b 
                  WHERE CAST(b.FID AS NVARCHAR(64)) = v.[F数据作用域ID]
              );";

        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await _context.Database.OpenConnectionAsync();
        try
        {
            var result = await command.ExecuteScalarAsync();
            var count = Convert.ToInt32(result);

            if (count > 0)
            {
                _logger.LogWarning("发现 {Count} 条可能孤立的凭证（关联批次已不存在），请人工核查", count);
            }
            else
            {
                _logger.LogInformation("未发现孤立凭证");
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// 检测孤立暂存行：暂存表中 F批次ID 不在 CF批次 中的记录（仅告警）
    /// </summary>
    private async Task DetectOrphanedStagingRowsAsync()
    {
        // 获取所有不同的实际暂存表名（直接查询 CF批次 表，避免依赖已废弃的 DcImportBatch 实体）
        var targetTables = await _context.Database
            .SqlQueryRaw<string>("SELECT DISTINCT [F实际暂存表] FROM [CF批次] WHERE [F实际暂存表] IS NOT NULL AND [F实际暂存表] <> ''")
            .ToListAsync();

        var totalOrphanedRows = 0;

        foreach (var tableName in targetTables)
        {
            try
            {
                // 安全校验表名（防止 SQL 注入），仅允许中文/字母/数字/下划线
                if (!IsValidTableName(tableName))
                {
                    _logger.LogWarning("跳过非法暂存表名: {TableName}", tableName);
                    continue;
                }

                var sql = $@"
                    SELECT COUNT(1) FROM [{tableName}] s
                    WHERE NOT EXISTS (
                        SELECT 1 FROM [CF批次] b WHERE b.FID = s.[F批次ID]
                    );";

                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = sql;
                await _context.Database.OpenConnectionAsync();
                try
                {
                    var result = await command.ExecuteScalarAsync();
                    var count = Convert.ToInt32(result);
                    if (count > 0)
                    {
                        totalOrphanedRows += count;
                        _logger.LogWarning("暂存表 [{TableName}] 中发现 {Count} 条孤立行（关联批次已不存在）",
                            tableName, count);
                    }
                }
                finally
                {
                    await _context.Database.CloseConnectionAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "检查暂存表 [{TableName}] 时出错，跳过", tableName);
            }
        }

        if (totalOrphanedRows > 0)
        {
            _logger.LogWarning("孤立暂存行检查完成，共发现 {Total} 条孤立行", totalOrphanedRows);
        }
        else
        {
            _logger.LogInformation("未发现孤立暂存行");
        }
    }

    /// <summary>验证表名合法性（防止SQL注入）</summary>
    private static bool IsValidTableName(string name)
    {
        // 允许中文、字母、数字、下划线
        return !string.IsNullOrWhiteSpace(name)
            && name.Length <= 200
            && Regex.IsMatch(name, @"^[\w\u4e00-\u9fff]+$");
    }
}
