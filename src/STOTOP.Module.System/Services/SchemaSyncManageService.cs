using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

public class SchemaSyncManageService : ISchemaSyncManageService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<SchemaSyncManageService> _logger;

    public SchemaSyncManageService(STOTOPDbContext dbContext, ILogger<SchemaSyncManageService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<SchemaSyncStatusDto> GetStatusAsync()
    {
        var result = new SchemaSyncStatusDto();

        // 查询待执行变更数
        var pendingCount = await _dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM [SYS_Schema同步记录] WHERE F状态 = N'待执行'")
            .FirstOrDefaultAsync();
        result.PendingCount = pendingCount;
        result.HasPendingChanges = pendingCount > 0;

        // 查询最近同步时间
        var lastTime = await _dbContext.Database
            .SqlQueryRaw<DateTime?>("SELECT MAX(F检测时间) AS [Value] FROM [SYS_Schema同步记录]")
            .FirstOrDefaultAsync();
        result.LastSyncTime = lastTime?.ToString("yyyy-MM-dd HH:mm:ss");

        // 查询 Seeder 版本状态：检查是否有失败记录
        var failCount = await _dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM [SYS迁移历史] WHERE F状态 <> N'Success'")
            .FirstOrDefaultAsync();
        result.SeederStatus = failCount > 0 ? $"有{failCount}项失败" : "全部已执行";

        return result;
    }

    public async Task<List<SchemaChangeItemDto>> GetPendingChangesAsync()
    {
        var items = await _dbContext.Database
            .SqlQueryRaw<SchemaChangeRaw>(
                @"SELECT FID, F表名, F列名, F变更类型, FSQL语句, F检测时间
                  FROM [SYS_Schema同步记录]
                  WHERE F状态 = N'待执行'
                  ORDER BY F检测时间 DESC")
            .ToListAsync();

        return items.Select(r => new SchemaChangeItemDto
        {
            Id = r.FID,
            TableName = r.F表名,
            ColumnName = r.F列名,
            ChangeType = r.F变更类型,
            SqlStatement = r.FSQL语句,
            DetectedAt = r.F检测时间.ToString("yyyy-MM-dd HH:mm:ss"),
        }).ToList();
    }

    public async Task ExecuteChangesAsync(List<long> changeIds, string? executedBy)
    {
        foreach (var id in changeIds)
        {
            // 获取 SQL 语句
            var sqlRow = await _dbContext.Database
                .SqlQueryRaw<string>(
                    "SELECT FSQL语句 AS [Value] FROM [SYS_Schema同步记录] WHERE FID = {0} AND F状态 = N'待执行'", id)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(sqlRow))
            {
                _logger.LogWarning("变更记录 {Id} 不存在或已执行", id);
                continue;
            }

            // 执行 SQL
            await _dbContext.Database.ExecuteSqlRawAsync(sqlRow);

            // 更新状态
            await _dbContext.Database.ExecuteSqlRawAsync(
                @"UPDATE [SYS_Schema同步记录] 
                  SET F状态 = N'已执行', F执行时间 = GETDATE(), F执行人 = {0}
                  WHERE FID = {1}",
                executedBy ?? "System", id);

            _logger.LogInformation("已执行 Schema 变更: {Id}", id);
        }
    }

    public async Task SkipChangesAsync(List<long> changeIds)
    {
        if (changeIds.Count == 0) return;

        // 将 Id 列表拼接为参数化更新
        foreach (var id in changeIds)
        {
            await _dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE [SYS_Schema同步记录] SET F状态 = N'已跳过' WHERE FID = {0}", id);
        }

        _logger.LogInformation("已跳过 {Count} 项 Schema 变更", changeIds.Count);
    }

    public async Task<List<SchemaWarningItemDto>> GetWarningsAsync()
    {
        // 警告：数据库中存在但 Entity 未定义的列由 SchemaAutoSync 引擎产生
        // 这里从 SYS_Schema同步记录 表中状态为 'Warning' 的记录读取
        var items = await _dbContext.Database
            .SqlQueryRaw<SchemaWarningRaw>(
                @"SELECT F表名, F列名, FSQL语句
                  FROM [SYS_Schema同步记录]
                  WHERE F变更类型 = N'Warning'
                  ORDER BY F表名, F列名")
            .ToListAsync();

        return items.Select(r => new SchemaWarningItemDto
        {
            TableName = r.F表名,
            ColumnName = r.F列名,
            Message = r.FSQL语句,
        }).ToList();
    }

    public async Task<(List<MigrationHistoryItemDto> Items, int Total)> GetHistoryAsync(int pageIndex, int pageSize)
    {
        var total = await _dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM [SYS迁移历史]")
            .FirstOrDefaultAsync();

        var offset = (pageIndex - 1) * pageSize;
        var items = await _dbContext.Database
            .SqlQueryRaw<MigrationHistoryRaw>(
                @"SELECT FID, F模块, F版本号, F描述, F状态, F执行时间, F耗时ms
                  FROM [SYS迁移历史]
                  ORDER BY F执行时间 DESC
                  OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY",
                offset, pageSize)
            .ToListAsync();

        var dtos = items.Select(r => new MigrationHistoryItemDto
        {
            Id = r.FID,
            Module = r.F模块,
            Version = r.F版本号,
            Description = r.F描述 ?? "",
            Status = r.F状态 ?? "Success",
            ExecutedTime = r.F执行时间.ToString("yyyy-MM-dd HH:mm:ss"),
            DurationMs = r.F耗时ms,
        }).ToList();

        return (dtos, total);
    }

    // === 内部 Raw 映射类型 ===

    internal class SchemaChangeRaw
    {
        public long FID { get; set; }
        public string F表名 { get; set; } = "";
        public string F列名 { get; set; } = "";
        public string F变更类型 { get; set; } = "";
        public string FSQL语句 { get; set; } = "";
        public DateTime F检测时间 { get; set; }
    }

    internal class SchemaWarningRaw
    {
        public string F表名 { get; set; } = "";
        public string F列名 { get; set; } = "";
        public string FSQL语句 { get; set; } = "";
    }

    internal class MigrationHistoryRaw
    {
        public long FID { get; set; }
        public string F模块 { get; set; } = "";
        public int F版本号 { get; set; }
        public string? F描述 { get; set; }
        public string? F状态 { get; set; }
        public DateTime F执行时间 { get; set; }
        public long? F耗时ms { get; set; }
    }
}
