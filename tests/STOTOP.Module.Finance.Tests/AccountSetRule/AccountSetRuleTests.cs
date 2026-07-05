using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Constants;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.Finance.Tests.Voucher;
using Xunit;

namespace STOTOP.Module.Finance.Tests.AccountSetRule;

/// <summary>
/// 账套规则(P0)：规则服务读写/回退语义 + P0-1 制单审核分离 + P0-3 凭证字白名单 的行为测试。
/// 铁律：无配置 = 现状（fail-safe，零行为变更）。
/// </summary>
public class AccountSetRuleTests
{
    private const long Org = 100;
    private const long AcctSet = 7;
    private const long OtherAcctSet = 8;

    private static (STOTOPDbContext db, AccountSetRuleService svc) CreateRuleService(string name)
    {
        var db = TestDbContextFactory.Create(name, orgId: Org);
        var http = VoucherServiceTestHarness.HttpContext(Org, AcctSet);
        return (db, VoucherServiceTestHarness.BuildRuleService(db, http));
    }

    // ===================== 规则服务：回退语义 =====================

    [Fact]
    public async global::System.Threading.Tasks.Task 无配置时_启用凭证字回退全集且含记()
    {
        var (db, svc) = CreateRuleService(nameof(无配置时_启用凭证字回退全集且含记));
        await using var _ = db;

        var words = await svc.GetEnabledVoucherWordsAsync(AcctSet);

        Assert.Equal(VoucherWord.AllWords, words);
        Assert.Contains(VoucherWord.Ji, words);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 无配置时_结转科目回退默认字面量()
    {
        var (db, svc) = CreateRuleService(nameof(无配置时_结转科目回退默认字面量));
        await using var _ = db;

        var (profit, retained) = await svc.GetClosingAccountCodesAsync(AcctSet);

        Assert.Equal("3103", profit);
        Assert.Equal("310405", retained);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 无配置时_Dto返回默认值()
    {
        var (db, svc) = CreateRuleService(nameof(无配置时_Dto返回默认值));
        await using var _ = db;

        var dto = await svc.GetDtoAsync(AcctSet);

        Assert.False(dto.FRequireAuditSeparation);
        Assert.Null(dto.FProfitAccountCode);
        Assert.Null(dto.FRetainedAccountCode);
        Assert.Equal(VoucherWord.AllWords, dto.FEnabledVoucherWords);
    }

    // ===================== 规则服务：Upsert =====================

    [Fact]
    public async global::System.Threading.Tasks.Task Upsert后_按账套读回一致_且凭证字强制含记()
    {
        var (db, svc) = CreateRuleService(nameof(Upsert后_按账套读回一致_且凭证字强制含记));
        await using var _ = db;
        db.Set<FinAccount>().AddRange(
            VoucherServiceTestHarness.Account(1, "3888", "自定义本年利润", AcctSet, Org),
            VoucherServiceTestHarness.Account(2, "310488", "自定义未分配利润", AcctSet, Org));
        await db.SaveChangesAsync();

        var saved = await svc.UpsertAsync(AcctSet, new UpdateAccountSetRuleRequest
        {
            FRequireAuditSeparation = true,
            FProfitAccountCode = "3888",
            FRetainedAccountCode = "310488",
            FEnabledVoucherWords = new List<string> { "收", "付" }, // 未含"记"，服务端须强制并入
        }, "tester");

        Assert.True(saved.FRequireAuditSeparation);
        Assert.Equal("3888", saved.FProfitAccountCode);
        Assert.Contains(VoucherWord.Ji, saved.FEnabledVoucherWords);

        var (profit, retained) = await svc.GetClosingAccountCodesAsync(AcctSet);
        Assert.Equal("3888", profit);
        Assert.Equal("310488", retained);

        // 再次 Upsert 走更新路径（一账套一行），不产生第二行
        await svc.UpsertAsync(AcctSet, new UpdateAccountSetRuleRequest
        {
            FRequireAuditSeparation = false,
            FEnabledVoucherWords = new List<string> { "记" },
        }, "tester");
        Assert.Single(db.Set<FinAccountSetRule>().Where(r => r.FAccountSetId == AcctSet));
        var dto = await svc.GetDtoAsync(AcctSet);
        Assert.False(dto.FRequireAuditSeparation);
        Assert.Equal(new List<string> { "记" }, dto.FEnabledVoucherWords);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Upsert_无效凭证字被拒()
    {
        var (db, svc) = CreateRuleService(nameof(Upsert_无效凭证字被拒));
        await using var _ = db;

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpsertAsync(AcctSet,
            new UpdateAccountSetRuleRequest { FEnabledVoucherWords = new List<string> { "记", "杂" } }, "tester"));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Upsert_结转科目在账套不存在被拒()
    {
        var (db, svc) = CreateRuleService(nameof(Upsert_结转科目在账套不存在被拒));
        await using var _ = db;

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpsertAsync(AcctSet,
            new UpdateAccountSetRuleRequest { FProfitAccountCode = "9999" }, "tester"));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 跨账套不串数据()
    {
        var (db, svc) = CreateRuleService(nameof(跨账套不串数据));
        await using var _ = db;
        db.Set<FinAccount>().AddRange(
            VoucherServiceTestHarness.Account(1, "3888", "利润A", AcctSet, Org),
            VoucherServiceTestHarness.Account(2, "3999", "利润B", OtherAcctSet, Org));
        await db.SaveChangesAsync();

        await svc.UpsertAsync(AcctSet, new UpdateAccountSetRuleRequest { FProfitAccountCode = "3888" }, "tester");
        await svc.UpsertAsync(OtherAcctSet, new UpdateAccountSetRuleRequest { FProfitAccountCode = "3999" }, "tester");

        var ruleA = await svc.GetByAccountSetAsync(AcctSet);
        var ruleB = await svc.GetByAccountSetAsync(OtherAcctSet);
        Assert.Equal("3888", ruleA!.FProfitAccountCode);
        Assert.Equal("3999", ruleB!.FProfitAccountCode);
    }

    // ===================== P0-1 制单审核分离 =====================

    private static async global::System.Threading.Tasks.Task<STOTOPDbContext> SeedVoucherBaseAsync(string name)
    {
        var db = TestDbContextFactory.Create(name, orgId: Org);
        db.Set<FinAccount>().AddRange(
            VoucherServiceTestHarness.Account(1, "1001", "库存现金", AcctSet, Org),
            VoucherServiceTestHarness.Account(2, "3001", "实收资本", AcctSet, Org));
        db.Set<FinAccountPeriod>().Add(VoucherServiceTestHarness.Period(11, 2026, 6, AcctSet));
        await db.SaveChangesAsync();
        return db;
    }

    private static CreateVoucherRequest VoucherRequest(string word = "记") => new()
    {
        VoucherWord = word,
        Date = new DateTime(2026, 6, 15),
        PeriodId = 0,
        Entries =
        {
            new CreateVoucherEntryRequest { LineNo = 1, Summary = "t", AccountId = 1, DebitAmount = 100m },
            new CreateVoucherEntryRequest { LineNo = 2, Summary = "t", AccountId = 2, CreditAmount = 100m },
        }
    };

    [Fact]
    public async global::System.Threading.Tasks.Task 开关关或无配置_制单人可审核本人凭证()
    {
        await using var db = await SeedVoucherBaseAsync(nameof(开关关或无配置_制单人可审核本人凭证));
        var svc = VoucherServiceTestHarness.Build(db, VoucherServiceTestHarness.HttpContext(Org, AcctSet, userName: "张三"));

        var created = await svc.CreateAsync(VoucherRequest(), "张三", AcctSet);

        // 无规则行 → 现状放行（零行为变更）
        Assert.True(await svc.AuditAsync(created.Id, "张三"));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 开关开_制单人审核本人凭证被拒_他人可审()
    {
        await using var db = await SeedVoucherBaseAsync(nameof(开关开_制单人审核本人凭证被拒_他人可审));
        var svc = VoucherServiceTestHarness.Build(db, VoucherServiceTestHarness.HttpContext(Org, AcctSet, userName: "张三"));

        db.Set<FinAccountSetRule>().Add(new FinAccountSetRule { FAccountSetId = AcctSet, FRequireAuditSeparation = true });
        await db.SaveChangesAsync();

        var created = await svc.CreateAsync(VoucherRequest(), "张三", AcctSet);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AuditAsync(created.Id, "张三"));
        Assert.Contains("制单人不可审核", ex.Message);

        // 他人审核放行
        Assert.True(await svc.AuditAsync(created.Id, "李四"));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 批量审核_制单人自审逐张跳过不整批失败()
    {
        await using var db = await SeedVoucherBaseAsync(nameof(批量审核_制单人自审逐张跳过不整批失败));
        var svc = VoucherServiceTestHarness.Build(db, VoucherServiceTestHarness.HttpContext(Org, AcctSet, userName: "张三"));

        db.Set<FinAccountSetRule>().Add(new FinAccountSetRule { FAccountSetId = AcctSet, FRequireAuditSeparation = true });
        await db.SaveChangesAsync();

        var mine = await svc.CreateAsync(VoucherRequest(), "张三", AcctSet);   // 审核人本人制单
        var others = await svc.CreateAsync(VoucherRequest(), "王五", AcctSet); // 他人制单

        var result = await svc.BatchAuditAsync(new List<long> { mine.Id, others.Id }, 1, "张三");

        Assert.Contains("制单人不可自审", result.Message);
        Assert.Contains("成功审核 1 张", result.Message);

        // 他人凭证已审、本人凭证保持未审
        Assert.Equal(2, db.Set<FinVoucher>().First(v => v.FID == others.Id).FStatus);
        Assert.NotEqual(2, db.Set<FinVoucher>().First(v => v.FID == mine.Id).FStatus);
    }

    // ===================== P0-3 凭证字白名单 =====================

    [Fact]
    public async global::System.Threading.Tasks.Task 无配置时_四凭证字均可建凭证()
    {
        await using var db = await SeedVoucherBaseAsync(nameof(无配置时_四凭证字均可建凭证));
        var svc = VoucherServiceTestHarness.Build(db, VoucherServiceTestHarness.HttpContext(Org, AcctSet));

        var created = await svc.CreateAsync(VoucherRequest("收"), "tester", AcctSet);
        Assert.True(created.Id > 0);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 启用子集后_停用凭证字新建被拒_草稿不校验()
    {
        await using var db = await SeedVoucherBaseAsync(nameof(启用子集后_停用凭证字新建被拒_草稿不校验));
        var svc = VoucherServiceTestHarness.Build(db, VoucherServiceTestHarness.HttpContext(Org, AcctSet));

        db.Set<FinAccountSetRule>().Add(new FinAccountSetRule
        {
            FAccountSetId = AcctSet,
            FEnabledVoucherWords = "[\"记\"]", // 仅启用"记"
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(VoucherRequest("收"), "tester", AcctSet));
        Assert.Contains("凭证字只能是", ex.Message);

        // "记"仍可建
        var ok = await svc.CreateAsync(VoucherRequest("记"), "tester", AcctSet);
        Assert.True(ok.Id > 0);

        // 草稿不校验（允许中间态）
        var draft = await svc.SaveDraftAsync(VoucherRequest("收"), "tester", AcctSet);
        Assert.True(draft.Id > 0);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 规则JSON脏数据_回退全集不炸()
    {
        var (db, svc) = CreateRuleService(nameof(规则JSON脏数据_回退全集不炸));
        await using var _ = db;

        db.Set<FinAccountSetRule>().Add(new FinAccountSetRule { FAccountSetId = AcctSet, FEnabledVoucherWords = "{bad json" });
        await db.SaveChangesAsync();

        var words = await svc.GetEnabledVoucherWordsAsync(AcctSet);
        Assert.Equal(VoucherWord.AllWords, words);
    }
}
