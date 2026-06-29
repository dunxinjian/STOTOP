using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Amoeba;

/// <summary>
/// 批次6 §7.8 种子自检 —— 直接解析出货种子(FinanceSeeder.cs 的 ReseedAmoebaTemplate3 块),
/// 校验 72 项损益项的结构与 V2 filters 正交性。验证"落库的"种子, 而非生成器内存对象,
/// 以堵住"内存字段对、SQL 漏列"的盲区(对抗式校验抓到的 F节点角色 漏列即属此类)。
/// </summary>
public class AmoebaSeedTemplate3Tests
{
    private sealed record Spec(string Code, HashSet<string> Directions, HashSet<string> Departments);
    private sealed record Item(long Fid, string Name, string Role, string? Category,
        string? ValueSource, string? SysDataSource, string? AllocMode, List<Spec> Specs, string? Formula);

    private static readonly List<Item> Items = ParseShippedSeed();

    // ── 定位仓库根(含 src 与 tests), 读出货种子 ──
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src")) &&
                Directory.Exists(Path.Combine(dir.FullName, "tests")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("未找到仓库根(含 src 与 tests 子目录)");
    }

    private static List<Item> ParseShippedSeed()
    {
        var path = Path.Combine(RepoRoot(), "src", "STOTOP.WebAPI", "Data", "Seeders", "FinanceSeeder.cs");
        var text = File.ReadAllText(path);
        var items = new List<Item>();
        const string marker = "INSERT INTO [FIN阿米巴损益项] (";
        int i = 0;
        while ((i = text.IndexOf(marker, i, StringComparison.Ordinal)) >= 0)
        {
            int colStart = i + marker.Length;
            int colEnd = text.IndexOf(')', colStart);
            var cols = text.Substring(colStart, colEnd - colStart)
                           .Split(',').Select(c => c.Trim().Trim('[', ']')).ToList();
            int valStart = text.IndexOf("VALUES (", colEnd, StringComparison.Ordinal) + "VALUES (".Length;
            var (vals, next) = ParseValues(text, valStart);
            i = next;
            // 列名数必须等于值数 —— 防"漏列/多值"静默错位(对抗式校验抓到的 F节点角色 漏列即此类)
            Assert.True(cols.Count == vals.Count, $"列数({cols.Count})≠值数({vals.Count}) @ {string.Join(",", vals.Take(2))}");
            var map = new Dictionary<string, string?>();
            for (int k = 0; k < cols.Count && k < vals.Count; k++) map[cols[k]] = vals[k];

            var role = map.GetValueOrDefault("F节点角色");
            var accJson = map.GetValueOrDefault("F关联科目JSON");
            items.Add(new Item(
                Fid: long.Parse(map["FID"]!),
                Name: map.GetValueOrDefault("F项目名称") ?? "",
                Role: role ?? "",
                Category: map.GetValueOrDefault("F项目类别"),
                ValueSource: map.GetValueOrDefault("F值来源"),
                SysDataSource: map.GetValueOrDefault("F系统数据源"),
                AllocMode: map.GetValueOrDefault("F分摊方式"),
                Specs: ParseSpecs(accJson),
                Formula: map.GetValueOrDefault("F计算公式")));
        }
        return items;
    }

    // 从 VALUES( 后逐字符解析, 尊重 N'...'(含 '' 与 "" 转义), 顶层逗号切分, 不在引号内的 ) 收尾
    private static (List<string?> vals, int next) ParseValues(string text, int start)
    {
        var vals = new List<string?>();
        var cur = new StringBuilder();
        bool inStr = false; bool token = false;
        int p = start;
        for (; p < text.Length; p++)
        {
            char c = text[p];
            if (inStr)
            {
                if (c == '\'')
                {
                    if (p + 1 < text.Length && text[p + 1] == '\'') { cur.Append('\''); p++; }
                    else inStr = false;
                }
                else cur.Append(c);
            }
            else if (c == '\'') { inStr = true; token = true; }   // 进入 N'...'(N 已被吞为非引号字符)
            else if (c == ',') { vals.Add(Norm(cur.ToString(), token)); cur.Clear(); token = false; }
            else if (c == ')') { vals.Add(Norm(cur.ToString(), token)); return (vals, p + 1); }
            else { if (!char.IsWhiteSpace(c) || cur.Length > 0) cur.Append(c); token = true; }
        }
        return (vals, p);
    }

    // token=true 且非纯 NULL/数字 → 是字符串值; 还原 C# 逐字串的 "" -> "
    private static string? Norm(string raw, bool token)
    {
        raw = raw.Trim();
        if (raw == "NULL" || raw.Length == 0) return null;
        // 去掉残留的 N 前缀标记(N'...' 的 N 在非引号态被收集); NULL 已先行返回, 不会误伤
        if (token && raw.StartsWith("N")) raw = raw.Substring(1).Trim();
        return raw.Replace("\"\"", "\"");
    }

    private static List<Spec> ParseSpecs(string? json)
    {
        var list = new List<Spec>();
        if (string.IsNullOrWhiteSpace(json)) return list;
        using var doc = JsonDocument.Parse(json);
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var dirs = new HashSet<string>(); var deps = new HashSet<string>();
            if (el.TryGetProperty("filters", out var filters))
                foreach (var f in filters.EnumerateArray())
                {
                    var at = f.GetProperty("auxType").GetString();
                    var codes = f.GetProperty("codes").EnumerateArray().Select(x => x.GetString()!).ToList();
                    if (at == "business_direction") foreach (var c in codes) dirs.Add(c);
                    if (at == "department") foreach (var c in codes) deps.Add(c);
                }
            list.Add(new Spec(el.GetProperty("code").GetString()!, dirs, deps));
        }
        return list;
    }

    [Fact]
    public void Seed_has_72_items_with_valid_roles_and_contiguous_fids()
    {
        Assert.Equal(72, Items.Count);
        Assert.Equal(72, Items.Select(x => x.Fid).Distinct().Count());
        Assert.Equal(600, Items.Min(x => x.Fid));
        Assert.Equal(671, Items.Max(x => x.Fid));
        var roles = new HashSet<string> { "group", "data", "indicator", "formula" };
        // F节点角色 必须显式落库(对抗式校验发现的 blocker: 漏列会全部默认 'data')
        Assert.All(Items, it => Assert.Contains(it.Role, roles));
        Assert.Equal(16, Items.Count(x => x.Role == "group"));
        Assert.Equal(42, Items.Count(x => x.Role == "data"));
        Assert.Equal(11, Items.Count(x => x.Role == "indicator"));
        Assert.Equal(3, Items.Count(x => x.Role == "formula"));
    }

    [Fact]
    public void Every_voucher_revenue_or_cost_leaf_has_related_accounts()  // P0-1
    {
        var bad = Items.Where(x => x.Role == "data" && x.ValueSource == "system"
                                   && x.SysDataSource == "voucher" && x.Specs.Count == 0).ToList();
        Assert.True(bad.Count == 0, "无关联科目的 voucher data 叶(恒0风险): " + string.Join(", ", bad.Select(b => b.Name)));
        // 每个 spec 的 code 非空
        Assert.All(Items.SelectMany(x => x.Specs), s => Assert.False(string.IsNullOrWhiteSpace(s.Code)));
    }

    [Fact]
    public void No_same_code_direction_overlap_among_voucher_leaves()  // §7.8 ValidateNoOverlap(部门/方向感知)
    {
        var vd = Items.Where(x => x.Role == "data" && x.SysDataSource == "voucher").ToList();
        var dupes = new List<string>();
        for (int a = 0; a < vd.Count; a++)
            for (int b = a + 1; b < vd.Count; b++)
                foreach (var sa in vd[a].Specs)
                    foreach (var sb in vd[b].Specs)
                    {
                        if (!(sa.Code.StartsWith(sb.Code) || sb.Code.StartsWith(sa.Code))) continue;
                        bool dirSep = sa.Directions.Count > 0 && sb.Directions.Count > 0 && !sa.Directions.Overlaps(sb.Directions);
                        bool depSep = sa.Departments.Count > 0 && sb.Departments.Count > 0 && !sa.Departments.Overlaps(sb.Departments);
                        if (!dirSep && !depSep)
                            dupes.Add($"{vd[a].Name}({sa.Code}) ↔ {vd[b].Name}({sb.Code})");
                    }
        Assert.True(dupes.Count == 0, "同码方向/部门重叠: " + string.Join("; ", dupes));
    }

    [Fact]
    public void Data_leaf_names_are_unique_and_formula_refs_resolve()
    {
        // data 叶名唯一(跨段重名须已加前缀, 如 出港/进港操作工资)
        var dup = Items.Where(x => x.Role == "data").GroupBy(x => x.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dup.Count == 0, "data 叶重名: " + string.Join(", ", dup));
        // 公式 ${名} 引用必须存在
        var names = Items.Select(x => x.Name).ToHashSet();
        foreach (var it in Items.Where(x => x.Role == "formula" && !string.IsNullOrEmpty(x.Formula)))
            foreach (Match m in Regex.Matches(it.Formula!, @"\$\{([^}]+)\}"))
                Assert.True(names.Contains(m.Groups[1].Value), $"公式 {it.Name} 引用不存在项 ${{{m.Groups[1].Value}}}");
    }

    [Fact]
    public void Common_cost_leaves_are_volume_and_others_are_direct()
    {
        // 房租/水电/折旧/摊销/管理费用/财务费用/操作工资 应为 volume
        foreach (var nm in new[] { "房租", "水电", "折旧", "摊销", "管理费用", "财务费用", "出港操作工资", "进港操作工资" })
        {
            var it = Items.Single(x => x.Name == nm);
            Assert.Equal("volume", it.AllocMode);
        }
    }
}
