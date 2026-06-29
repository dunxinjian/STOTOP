using System.Data;

namespace STOTOP.Module.CardFlow.AutoPlugin.Implementations;

/// <summary>
/// 跨批次去重的复合键构造与候选临时表装配（支持任意列数）。
/// 抽成纯函数以便离线单测，并保证「查询侧 SELECT 回的键」与「内存比对侧的键」用同一套规则。
/// 旧实现把去重键写死成二元组、临时表只填前两列，导致 ≥3 个去重字段时第三列恒 NULL、
/// JOIN 永不命中、整批被唯一索引冲突放弃——此类型按 fields.Count 全程动态化以修复该 bug。
/// </summary>
public static class CrossBatchDedupKeyBuilder
{
    // Unit Separator（U+001F）。业务文本（运单号/问题类型/日期等）不会出现该控制符，
    // 用作复合键字段分隔符，避免 ["AB","C"] 与 ["A","BC"] 拼接后撞键。
    public static readonly string FieldSeparator = ((char)0x1F).ToString();

    /// <summary>按字段顺序用 <see cref="FieldSeparator"/> 拼接成复合键。</summary>
    public static string BuildKey(IReadOnlyList<string> values)
        => string.Join(FieldSeparator, values);

    /// <summary>
    /// 从数据行按 <paramref name="fields"/> 顺序取值并 Trim，DBNull/缺值取空串。
    /// 这是去重键「取值规范化」的唯一口径——候选键构造与内存比对必须共用本方法，
    /// 否则两侧口径漂移会让复合键 Contains 静默漏配。
    /// </summary>
    public static string[] ExtractValues(DataRow row, IReadOnlyList<string> fields)
        => fields.Select(f => row[f]?.ToString()?.Trim() ?? string.Empty).ToArray();

    /// <summary>
    /// 按 <paramref name="fields"/> 顺序逐列装配候选值临时表，缺位补空串，并按完整复合键去重。
    /// 关键：严格按 fields.Count 建列并逐列填值，绝不漏填第三列及以后（旧 bug 根因）。
    /// </summary>
    public static DataTable BuildCandidateTable(
        IReadOnlyList<string> fields, IEnumerable<string[]> valueSets)
    {
        var table = new DataTable();
        foreach (var f in fields)
            table.Columns.Add(f, typeof(string));

        if (fields.Count == 0)
            return table;

        var seen = new HashSet<string>();
        foreach (var raw in valueSets)
        {
            var values = new string[fields.Count];
            for (int i = 0; i < fields.Count; i++)
                values[i] = i < raw.Length ? raw[i] ?? string.Empty : string.Empty;

            if (!seen.Add(BuildKey(values)))
                continue; // 完整复合键重复 → 跳过

            var row = table.NewRow();
            for (int i = 0; i < fields.Count; i++)
                row[i] = values[i];
            table.Rows.Add(row);
        }

        return table;
    }
}
