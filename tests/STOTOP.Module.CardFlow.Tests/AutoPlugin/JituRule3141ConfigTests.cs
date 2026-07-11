// 极兔凭证规则 3141（财务确认版, Resources/jitu-hqtx-rule3141.json）配置回归测试。
// 钉住生成器的关键约定，防止后续重生成/手改破坏引擎语义：
//   - 155 组、组间 exactCategories 唯一（Layer2 精确匹配 F费用子类，重复=只首组命中）
//   - 四行收支模式：收入对(L3/L4)必须挂 F交易类型=加款 条件行——同方向兜底行首个吃光剩余行，
//     无条件的第二兜底行恒空（AutoVoucherMatchingEngineV2.AssignWithinDirection 语义）
//   - 每组支出侧借贷对称（借业务/贷220201）、金额列仅收支双列、FID 全有效
//   - 重名子类型合并组（漏扫扣款/上传不及时扣款）业务行按 F费用主类 条件分流
// 纯 JSON 解析，离线可跑。

using System.Text.Json;
using STOTOP.Module.CardFlow.Models;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.AutoPlugin;

public class JituRule3141ConfigTests
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    private static RulesBasedVoucherConfigV2 Load()
    {
        // 从测试输出目录向上定位仓库根（src 与 tests 同级）
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "src", "STOTOP.WebAPI", "Data", "Seeders", "Resources", "jitu-hqtx-rule3141.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, "src", "STOTOP.WebAPI", "Data", "Seeders", "Resources", "jitu-hqtx-rule3141.json");
        var cfg = JsonSerializer.Deserialize<RulesBasedVoucherConfigV2>(File.ReadAllText(path), Opts);
        Assert.NotNull(cfg);
        return cfg!;
    }

    [Fact]
    public void 顶层配置与骨架口径一致()
    {
        var cfg = Load();
        Assert.Equal("rulesBased", cfg.Mode);
        Assert.Equal(2, cfg.Version);
        Assert.Equal("F业务日期", cfg.DateField);
        Assert.Equal("STG极兔总部交易明细", cfg.StagingTable);
        Assert.Equal(2, cfg.AccountSetId);
        Assert.Null(cfg.GroupBy); // GroupBy 空 → 按(规则组×业务日期)拆凭证，批次=单网点文件 → 网点×日×子类型粒度
        Assert.Equal("createDraft", cfg.UnmatchedAction);
        Assert.Equal(700044, cfg.DraftPlaceholderAccountId); // 1901 待处理财产损溢
        Assert.Equal(new[] { "F网点编号", "F业务日期", "F费用子类" }, cfg.KeyFields!);
        Assert.Equal("F费用子类", cfg.MatchingLayers.CategoryField);
    }

    [Fact]
    public void 组数155且exactCategories唯一()
    {
        var cfg = Load();
        Assert.Equal(155, cfg.RuleGroups.Count);
        var cats = cfg.RuleGroups.Select(g => Assert.Single(g.ExactCategories!)).ToList();
        Assert.Equal(cats.Count, cats.Distinct().Count());
        Assert.All(cfg.RuleGroups, g => Assert.Equal("SUM", g.AmountAggregation));
        Assert.All(cfg.RuleGroups, g => Assert.False(g.Fallthrough));
    }

    [Fact]
    public void 每组支出侧借贷对称且FID有效()
    {
        var cfg = Load();
        foreach (var g in cfg.RuleGroups)
        {
            Assert.All(g.Lines, l => Assert.True(l.AccountId is > 0, $"{g.Name} 行缺FID"));
            Assert.All(g.Lines, l => Assert.Contains(l.AmountField, new[] { "F发生额收入", "F发生额支出" }));
            // 支出侧：≥1 借业务行 + 恰 1 贷220201 兜底行
            var expenseDebits = g.Lines.Where(l => l.Direction == "借" && l.AmountField == "F发生额支出").ToList();
            var expenseCredits = g.Lines.Where(l => l.Direction == "贷" && l.AmountField == "F发生额支出").ToList();
            Assert.True(expenseDebits.Count >= 1, $"{g.Name} 无支出借行");
            var cp = Assert.Single(expenseCredits);
            Assert.Equal(700125, cp.AccountId); // 220201 总部应付
            Assert.Null(cp.ConditionField);     // 对手兜底行不挂条件
        }
    }

    [Fact]
    public void 收入对必须是加款条件行_防兜底吃光语义坑()
    {
        var cfg = Load();
        var fourLineGroups = cfg.RuleGroups.Where(g => g.Lines.Count == 4).ToList();
        Assert.True(fourLineGroups.Count >= 150, $"四行组仅 {fourLineGroups.Count}");
        foreach (var g in fourLineGroups)
        {
            var incomeLines = g.Lines.Where(l => l.AmountField == "F发生额收入").ToList();
            Assert.Equal(2, incomeLines.Count);
            Assert.All(incomeLines, l =>
            {
                Assert.Equal("F交易类型", l.ConditionField);
                Assert.Equal(new List<string> { "加款" }, l.ConditionValues);
            });
            // 收入对方向：贷业务 + 借220201
            Assert.Contains(incomeLines, l => l.Direction == "贷" && l.AccountId != 700125);
            Assert.Contains(incomeLines, l => l.Direction == "借" && l.AccountId == 700125);
        }
    }

    [Fact]
    public void 重名子类型合并组按费用主类条件分流()
    {
        var cfg = Load();
        foreach (var sub in new[] { "漏扫扣款", "上传不及时扣款" })
        {
            var g = Assert.Single(cfg.RuleGroups, x => x.ExactCategories![0] == sub);
            var bizLines = g.Lines.Where(l => l.AccountId != 700125).ToList();
            Assert.Equal(2, bizLines.Count); // 客服类/质控类 各一行
            Assert.All(bizLines, l => Assert.Equal("F费用主类", l.ConditionField));
            Assert.NotEqual(bizLines[0].AccountId, bizLines[1].AccountId); // 映射到不同科目
            Assert.NotEqual(bizLines[0].ConditionValues![0], bizLines[1].ConditionValues![0]);
        }
    }

    [Fact]
    public void 资金类子类型不建组_走createDraft()
    {
        var cfg = Load();
        var cats = cfg.RuleGroups.SelectMany(g => g.ExactCategories!).ToHashSet();
        foreach (var fund in new[] { "提现", "转出", "转入", "风险保证金", "质量保证金", "质量保证金调整", "线上充值" })
            Assert.DoesNotContain(fund, cats);
    }
}
