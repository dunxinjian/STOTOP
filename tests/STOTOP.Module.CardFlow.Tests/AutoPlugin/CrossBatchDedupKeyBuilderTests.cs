// 跨批次去重复合键 / 候选表构造——纯函数单测（离线可跑）
// 钉死框架级 bug：3 列及以上去重字段时，旧二元组实现把第三列丢失、临时表第三列恒 NULL。
using System.Data;
using STOTOP.Module.CardFlow.AutoPlugin.Implementations;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.AutoPlugin;

public class CrossBatchDedupKeyBuilderTests
{
    private static readonly string[] ThreeFields = { "F运单号", "F问题类型", "F统计日期" };

    // ── BuildKey ───────────────────────────────────────────────────────

    [Fact]
    public void BuildKey_distinguishes_rows_differing_only_in_third_field()
    {
        // 旧二元组只取前两列，仅第三列不同的两行会被当成同一键 → 去重误判。
        var keyA = CrossBatchDedupKeyBuilder.BuildKey(new[] { "SF123", "破损", "20260617" });
        var keyB = CrossBatchDedupKeyBuilder.BuildKey(new[] { "SF123", "破损", "20260618" });

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void BuildKey_is_stable_for_identical_values()
    {
        var key1 = CrossBatchDedupKeyBuilder.BuildKey(new[] { "SF123", "破损", "20260617" });
        var key2 = CrossBatchDedupKeyBuilder.BuildKey(new[] { "SF123", "破损", "20260617" });

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void BuildKey_does_not_collide_on_field_boundary_shift()
    {
        // 分隔符必须真正隔离字段：["AB","C"] 与 ["A","BC"] 不能撞键。
        var key1 = CrossBatchDedupKeyBuilder.BuildKey(new[] { "AB", "C" });
        var key2 = CrossBatchDedupKeyBuilder.BuildKey(new[] { "A", "BC" });

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void BuildKey_single_field_equals_value()
    {
        var key = CrossBatchDedupKeyBuilder.BuildKey(new[] { "SF123" });

        Assert.Equal("SF123", key);
    }

    // ── BuildCandidateTable ─────────────────────────────────────────────

    [Fact]
    public void BuildCandidateTable_three_fields_populates_all_columns_without_nulls()
    {
        // 修复核心：旧实现只填 fields[0]/[1]，第三列恒 NULL → JOIN 永不命中。
        var values = new List<string[]>
        {
            new[] { "SF123", "破损", "20260617" },
            new[] { "SF124", "丢件", "20260617" },
        };

        var table = CrossBatchDedupKeyBuilder.BuildCandidateTable(ThreeFields, values);

        Assert.Equal(3, table.Columns.Count);
        Assert.Equal("F统计日期", table.Columns[2].ColumnName);
        Assert.Equal(2, table.Rows.Count);
        foreach (DataRow row in table.Rows)
            for (int i = 0; i < 3; i++)
                Assert.False(row.IsNull(i), $"第 {i} 列不应为 NULL");
        Assert.Equal("20260617", table.Rows[0]["F统计日期"]);
        Assert.Equal("丢件", table.Rows[1]["F问题类型"]);
    }

    [Fact]
    public void BuildCandidateTable_keeps_rows_differing_only_in_third_field_distinct()
    {
        var values = new List<string[]>
        {
            new[] { "SF123", "破损", "20260617" },
            new[] { "SF123", "破损", "20260618" }, // 仅第三列不同
        };

        var table = CrossBatchDedupKeyBuilder.BuildCandidateTable(ThreeFields, values);

        Assert.Equal(2, table.Rows.Count); // 旧二元组 Distinct 会错误合并成 1 行
    }

    [Fact]
    public void BuildCandidateTable_dedups_fully_identical_tuples()
    {
        var values = new List<string[]>
        {
            new[] { "SF123", "破损", "20260617" },
            new[] { "SF123", "破损", "20260617" }, // 完全重复
        };

        var table = CrossBatchDedupKeyBuilder.BuildCandidateTable(ThreeFields, values);

        Assert.Single(table.Rows);
    }

    [Fact]
    public void BuildCandidateTable_single_field_dedups_correctly()
    {
        var fields = new[] { "F运单号" };
        var values = new List<string[]>
        {
            new[] { "SF123" }, new[] { "SF124" }, new[] { "SF123" },
        };

        var table = CrossBatchDedupKeyBuilder.BuildCandidateTable(fields, values);

        Assert.Single(table.Columns);
        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public void BuildCandidateTable_zero_fields_returns_empty_table()
    {
        var table = CrossBatchDedupKeyBuilder.BuildCandidateTable(
            global::System.Array.Empty<string>(), new List<string[]>());

        Assert.Empty(table.Columns);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public void BuildCandidateTable_pads_short_value_arrays_with_empty()
    {
        // 候选行字段数少于 fields 时缺位补空串，不抛 IndexOutOfRange（防御）。
        var fields = new[] { "A", "B", "C" };
        var values = new List<string[]> { new[] { "x", "y" } };

        var table = CrossBatchDedupKeyBuilder.BuildCandidateTable(fields, values);

        Assert.Single(table.Rows);
        Assert.Equal(string.Empty, table.Rows[0]["C"]);
    }

    // ── ExtractValues ───────────────────────────────────────────────────

    [Fact]
    public void ExtractValues_trims_and_follows_fields_order()
    {
        var dt = new DataTable();
        dt.Columns.Add("F运单号");
        dt.Columns.Add("F问题类型");
        dt.Columns.Add("F统计日期");
        var row = dt.NewRow();
        row["F运单号"] = "  SF123 ";
        row["F问题类型"] = "破损";
        row["F统计日期"] = " 20260617";
        dt.Rows.Add(row);

        // 只取其中两列且乱序——结果须按 fields 顺序、且各值已 Trim
        var values = CrossBatchDedupKeyBuilder.ExtractValues(row, new[] { "F统计日期", "F运单号" });

        Assert.Equal(new[] { "20260617", "SF123" }, values);
    }

    [Fact]
    public void ExtractValues_dbnull_becomes_empty_string()
    {
        var dt = new DataTable();
        dt.Columns.Add("A");
        var row = dt.NewRow(); // A 为 DBNull
        dt.Rows.Add(row);

        var values = CrossBatchDedupKeyBuilder.ExtractValues(row, new[] { "A" });

        Assert.Equal(new[] { string.Empty }, values);
    }
}
