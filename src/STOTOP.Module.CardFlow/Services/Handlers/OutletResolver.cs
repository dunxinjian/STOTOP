using System.Data.Common;
using Dapper;

namespace STOTOP.Module.CardFlow.Services.Handlers;

/// <summary>
/// 网点名称解析器：把任意"网点名称/简称/全称/别名/包含子串"输入解析为唯一的网点编号。
/// 解析顺序：
///   1) 精确匹配 EXP快递网点.F网点简称 / F网点全称 / F编号
///   2) 精确匹配 EXP快递网点名称映射.F名称
///   3) 包含匹配（输入.Contains(简称|全称)，按 Key 长度从长到短，避免"城区"覆盖"城区东路"场景的歧义）
/// 使用方式：在每次批次处理入口一次性 LoadAsync，后续查询全内存。
/// </summary>
internal sealed class OutletResolver
{
    // 精确匹配字典：简称/全称/编号/别名 → 网点编号
    private readonly Dictionary<string, string> _exactToCode = new(StringComparer.OrdinalIgnoreCase);
    // 用于包含匹配的 Key 列表（长度降序），元素：(Key, Code)
    private readonly List<(string Key, string Code)> _fuzzyKeys = new();
    // 编号 → 简称（方便规则用简称做 value 时的回查）
    private readonly Dictionary<string, string> _codeToShortName = new(StringComparer.OrdinalIgnoreCase);

    public int PointCount { get; private set; }
    public int AliasCount { get; private set; }

    public static async Task<OutletResolver> LoadAsync(DbConnection connection, long orgId)
    {
        var resolver = new OutletResolver();

        // 1) 网点主表
        const string pointSql = @"
            SELECT [F编号] AS Code, [F网点简称] AS ShortName, [F网点全称] AS FullName
            FROM [EXP快递网点]
            WHERE [F状态] = 1 AND ([F组织ID] = @OrgId OR [F组织ID] = 0 OR [F所属组织ID] = @OrgId OR [F所属组织ID] = 0)";

        var points = (await connection.QueryAsync<(string Code, string? ShortName, string? FullName)>(
            pointSql, new { OrgId = orgId })).ToList();

        foreach (var p in points)
        {
            if (string.IsNullOrWhiteSpace(p.Code)) continue;

            // 编号自身也纳入精确字典
            resolver._exactToCode[p.Code] = p.Code;

            if (!string.IsNullOrWhiteSpace(p.ShortName))
            {
                resolver._exactToCode[p.ShortName!] = p.Code;
                resolver._fuzzyKeys.Add((p.ShortName!, p.Code));
                resolver._codeToShortName[p.Code] = p.ShortName!;
            }
            if (!string.IsNullOrWhiteSpace(p.FullName))
            {
                resolver._exactToCode.TryAdd(p.FullName!, p.Code);
                resolver._fuzzyKeys.Add((p.FullName!, p.Code));
            }
        }
        resolver.PointCount = points.Count;

        // 2) 别名表
        const string aliasSql = @"
            SELECT [F名称] AS Name, [F网点编号] AS Code
            FROM [EXP快递网点名称映射]
            WHERE [F组织ID] = @OrgId OR [F组织ID] = 0";

        var aliases = (await connection.QueryAsync<(string Name, string Code)>(
            aliasSql, new { OrgId = orgId })).ToList();

        foreach (var a in aliases)
        {
            if (string.IsNullOrWhiteSpace(a.Name) || string.IsNullOrWhiteSpace(a.Code)) continue;
            resolver._exactToCode.TryAdd(a.Name, a.Code);
        }
        resolver.AliasCount = aliases.Count;

        // 模糊匹配 Key 按长度降序排序，保证长词优先（避免"城区"先于"城区东部"匹配）
        resolver._fuzzyKeys.Sort((x, y) => y.Key.Length.CompareTo(x.Key.Length));

        return resolver;
    }

    /// <summary>
    /// 尝试将任意输入解析为网点编号。返回 false 表示无法识别。
    /// </summary>
    public bool TryResolve(string? input, out string code)
    {
        code = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        if (_exactToCode.TryGetValue(input, out var exact))
        {
            code = exact;
            return true;
        }

        foreach (var (key, keyCode) in _fuzzyKeys)
        {
            if (key.Length < 2) continue;
            if (input.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                code = keyCode;
                return true;
            }
        }
        return false;
    }

    /// <summary>根据网点编号查网点简称；用于规则 value 是简称时的二次匹配。</summary>
    public string? GetShortName(string code) =>
        _codeToShortName.TryGetValue(code, out var sn) ? sn : null;
}
