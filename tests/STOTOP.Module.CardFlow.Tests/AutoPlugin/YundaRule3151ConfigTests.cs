// 韵达凭证规则 3151（财务确认版, Resources/yunda-hqtx-rule3151.json, CardFlowSeeder V77）配置回归测试。
// 钉住财务确认版关键约定，防止后续重生成/手改破坏引擎语义：
//   - 77 组、Layer1 精确匹配 F三级科目编码（exactCodes 唯一）、amountAggregation=ROW；
//   - 财务 21 处改动：11 账户改码（含 5 处 往来→损益 重分类）+ 10 处进出港 BD；
//   - 每组两行借贷对称（业务行 + 对手 220201 总部应付 700125 + express_brand=YD）、金额列仅收支双列、FID 全有效；
//   - 资金类 282-x 不建组 → createDraft。
// 纯 JSON 解析，离线可跑（蓝本=JituRule3141ConfigTests）。改规则须同步本测试。
// 源=韵达交易-科目映射建议-已修改7.11.xlsx。

using System.Text.Json;
using STOTOP.Module.CardFlow.Models;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.AutoPlugin;

public class YundaRule3151ConfigTests
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    private static RulesBasedVoucherConfigV2 Load()
    {
        // Content 拷到输出目录（含 -o scratch）优先；源码树兜底（默认 bin 与 src 同盘时可达）。
        var rel = Path.Combine("Data", "Seeders", "Resources", "yunda-hqtx-rule3151.json");
        var candidates = new List<string> { Path.Combine(AppContext.BaseDirectory, rel) };
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            candidates.Add(Path.Combine(dir.FullName, "src", "STOTOP.WebAPI", rel));
        var path = candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("未找到 yunda-hqtx-rule3151.json（bin 或源码树）");
        var cfg = JsonSerializer.Deserialize<RulesBasedVoucherConfigV2>(File.ReadAllText(path), Opts);
        Assert.NotNull(cfg);
        return cfg!;
    }

    private static RuleGroupV2 ByCode(RulesBasedVoucherConfigV2 cfg, string code) =>
        Assert.Single(cfg.RuleGroups, g => g.ExactCodes != null && g.ExactCodes.Contains(code));

    private static string? Bd(EntryLineV2 line) =>
        line.AuxiliaryConfigs?.FirstOrDefault(a => a.AuxType == "business_direction")?.FixedValue;

    [Fact]
    public void 顶层配置口径一致()
    {
        var cfg = Load();
        Assert.Equal("rulesBased", cfg.Mode);
        Assert.Equal(2, cfg.Version);
        Assert.Equal("F交易日期", cfg.DateField);
        Assert.Equal("STG韵达总部交易明细", cfg.StagingTable);
        Assert.Equal(2, cfg.AccountSetId);
        Assert.Equal("createDraft", cfg.UnmatchedAction);
        Assert.Equal(700044, cfg.DraftPlaceholderAccountId);
        Assert.Equal("F三级科目编码", cfg.MatchingLayers.ExactMatchField);
        Assert.Equal(77, cfg.RuleGroups.Count);
        // Layer1 精确码唯一
        var codes = cfg.RuleGroups.SelectMany(g => g.ExactCodes!).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(cfg.RuleGroups, g => Assert.Equal("ROW", g.AmountAggregation));
    }

    [Theory]
    // 财务 5 处 往来→损益 重分类：新 FID + 业务行方向
    [InlineData("266-12", 700382L, "借")] // 仲裁代收代付→客服赔款(成本)
    [InlineData("275-10", 700382L, "借")] // 智橙网仲裁代收代付→客服赔款(成本)
    [InlineData("268-3", 700189L, "贷")]  // 共创基金→其他收入
    [InlineData("268-4", 700189L, "贷")]  // 复工保证金→其他收入
    [InlineData("320-4", 700307L, "贷")]  // 裹裹代收代付→总部平台单(收入)
    // 财务 6 处仅换科目
    [InlineData("1034-1", 700245L, "借")] // 快递柜
    [InlineData("107-1", 700341L, "借")]  // 面单费
    [InlineData("215-8", 700323L, "贷")]  // 政策考核
    [InlineData("284-14", 700366L, "借")] // 网格仓服务费
    [InlineData("310-2", 700343L, "借")]  // 补贴派费
    [InlineData("311-1", 700312L, "贷")]  // 调整派费
    public void 财务改码已回填(string code, long expectAcct, string expectDir)
    {
        var g = ByCode(Load(), code);
        Assert.Equal(expectAcct, g.Lines[0].AccountId);
        Assert.Equal(expectDir, g.Lines[0].Direction);
    }

    [Theory]
    [InlineData("107-1", "OUT")]
    [InlineData("311-1", "IN")]
    [InlineData("267-12", "OUT")]
    [InlineData("268-6", "IN")]
    [InlineData("291-8", "IN")]
    [InlineData("318-6", "OUT")]
    [InlineData("318-7", "OUT")]
    public void 财务BD调整已回填(string code, string expectBd)
    {
        var g = ByCode(Load(), code);
        Assert.Equal(expectBd, Bd(g.Lines[0]));
    }

    [Fact]
    public void 重分类组业务行补齐四维辅助核算()
    {
        // 往来→损益后，业务行须带 outlet/express_brand/business_direction/business_unit 四维（原往来行 aux 空）
        var cfg = Load();
        foreach (var code in new[] { "266-12", "268-3", "320-4" })
            Assert.Equal(4, ByCode(cfg, code).Lines[0].AuxiliaryConfigs!.Count);
    }

    [Fact]
    public void 全组不变量_两行借贷对称_FID有效_金额仅收支双列()
    {
        var cfg = Load();
        foreach (var g in cfg.RuleGroups)
        {
            Assert.Equal(2, g.Lines.Count);
            Assert.All(g.Lines, l => Assert.True(l.AccountId is > 0, $"{g.Name} 行缺FID"));
            Assert.All(g.Lines, l => Assert.Contains(l.AmountField, new[] { "F发生额收入", "F发生额支出" }));
            var a = g.Lines[0]; var b = g.Lines[1];
            Assert.Equal(a.AmountField, b.AmountField);                       // 同取数列 → 借贷等额
            Assert.Equal(new HashSet<string> { "借", "贷" },
                         new HashSet<string> { a.Direction!, b.Direction! }); // 一借一贷
        }
    }

    [Fact]
    public void 对手行恒为总部应付220201加YD品牌()
    {
        var cfg = Load();
        foreach (var g in cfg.RuleGroups)
        {
            var cp = g.Lines[1];
            Assert.Equal(700125, cp.AccountId); // 220201 总部应付
            Assert.Contains(cp.AuxiliaryConfigs!, a => a.AuxType == "express_brand" && a.FixedValue == "YD");
        }
    }

    [Fact]
    public void 资金类子类型不建组_走createDraft()
    {
        var cfg = Load();
        var codes = cfg.RuleGroups.SelectMany(g => g.ExactCodes!).ToHashSet();
        foreach (var fund in new[] { "282-6", "282-7", "282-8" }) // 提现/充值/划账
            Assert.DoesNotContain(fund, codes);
    }
}
