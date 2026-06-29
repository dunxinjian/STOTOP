using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;

namespace STOTOP.Module.Quality.Services.Unification;

/// <summary>
/// STG 暂存表 raw-SQL 按列名读取器（归一通用事件路径专用，轻量自建、不跨模块复用 CardFlow 的 StagingService）。
///
/// 为什么用 raw-SQL 而非 EF 投影：通用事件路径的列名来自描述符（运行期才确定），无法静态写 <c>.Select(r =&gt; new {...})</c>；
/// 而读全实体会碰到 STG 表允许 NULL、但实体声明为非空 long 的系统列（如 F账套ID NULL → SqlNullValueException）。
/// 故按描述符给定的列名集合，拼 <c>SELECT [col],... FROM [表] WHERE ...</c> 只投影需要的业务列（全部按字符串取）。
///
/// 安全：表名/列名均来自<b>静态源映射</b>（<see cref="ShentongSourceMap"/>），非用户输入；但仍做防御性正则白名单校验
/// （表名 <c>^STG…</c>、列名仅中文/字母/数字/下划线），值一律用 <see cref="DbParameter"/> 参数化。
/// </summary>
public sealed class StgRawReader
{
    private readonly STOTOPDbContext _db;

    // 表名：必须 STG 开头，仅含中文/字母/数字/下划线（与 CardFlow StagingService.ValidateTableName 同口径）。
    private static readonly Regex TableNameRegex = new(@"^STG[一-龥A-Za-z0-9_]+$", RegexOptions.Compiled);
    // 列名：中文/字母/数字/下划线（不要求前缀），首字符不可为数字（与 StagingService 字段名校验同口径）。
    private static readonly Regex ColumnNameRegex = new(@"^[A-Za-z一-龥_][A-Za-z0-9一-龥_]*$", RegexOptions.Compiled);

    public StgRawReader(STOTOPDbContext db) => _db = db;

    /// <summary>
    /// 按列名集合读取某 STG 表的行（仅 FOrgId 匹配且未撤销；可选「列=值」过滤）。
    /// </summary>
    /// <param name="tableName">STG 表名（来自静态源映射）。</param>
    /// <param name="columns">需要读的列名集合（自动去重、剔 null/空白；至少含 FID/F批次ID 由调用方保证）。</param>
    /// <param name="orgId">组织 ID（参数化）。</param>
    /// <param name="filterColumn">可选「仅X」过滤列名（与 filterValue 配对；任一为 null 则不过滤）。</param>
    /// <param name="filterValue">可选「仅X」过滤值（参数化）。</param>
    /// <returns>每行一个 列名→值 字典（值按字符串/原始类型取，NULL → null）。</returns>
    public async Task<List<Dictionary<string, object?>>> ReadAsync(
        string tableName,
        IEnumerable<string> columns,
        long orgId,
        string? filterColumn = null,
        string? filterValue = null,
        CancellationToken ct = default)
    {
        // ── 防御校验（表名/列名来自静态 map，仍白名单校验，杜绝任何拼接注入面）──
        if (!TableNameRegex.IsMatch(tableName))
            throw new ArgumentException($"非法 STG 表名: {tableName}", nameof(tableName));

        var cols = columns
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();
        if (cols.Count == 0)
            throw new ArgumentException("列名集合为空", nameof(columns));
        foreach (var c in cols)
            if (!ColumnNameRegex.IsMatch(c))
                throw new ArgumentException($"非法列名: {c}", nameof(columns));

        // 过滤列（若启用）也校验
        var useFilter = !string.IsNullOrWhiteSpace(filterColumn) && filterValue != null;
        if (useFilter && !ColumnNameRegex.IsMatch(filterColumn!))
            throw new ArgumentException($"非法过滤列名: {filterColumn}", nameof(filterColumn));

        var selectList = string.Join(",", cols.Select(c => $"[{c}]"));
        var sql = $"SELECT {selectList} FROM [{tableName}] WHERE [FOrgId] = @org AND [FIsRevoked] = 0";
        if (useFilter)
            sql += " AND [" + filterColumn + "] = @flt";

        var conn = _db.Database.GetDbConnection();
        bool openedHere = false;
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
            openedHere = true;
        }

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            var orgParam = cmd.CreateParameter();
            orgParam.ParameterName = "@org";
            orgParam.Value = orgId;
            cmd.Parameters.Add(orgParam);

            if (useFilter)
            {
                var fltParam = cmd.CreateParameter();
                fltParam.ParameterName = "@flt";
                fltParam.Value = (object?)filterValue ?? DBNull.Value;
                cmd.Parameters.Add(fltParam);
            }

            var result = new List<Dictionary<string, object?>>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var dict = new Dictionary<string, object?>(reader.FieldCount);
                for (var i = 0; i < reader.FieldCount; i++)
                    dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                result.Add(dict);
            }
            return result;
        }
        finally
        {
            // 只关闭本方法打开的连接（沿用既有连接时不强制关，避免影响外层 DbContext 事务/状态）。
            if (openedHere && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }
}
