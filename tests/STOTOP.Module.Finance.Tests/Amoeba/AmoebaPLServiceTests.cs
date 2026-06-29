using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using STOTOP.Module.Finance.Services.FormulaEngine;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Amoeba;

public class AmoebaPLServiceTests
{
    [Fact]
    public async Task GetPLItemDetail_applies_account_level_auxiliary_filters()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetPLItemDetail_applies_account_level_auxiliary_filters), orgId: 192);
        await SeedTemplateAndItemAsync(db, plItemId: 100, relatedAccountsJson: """[{"code":"5601","filters":[{"auxType":"outlet","codes":["OUT-A"]}]}]""");
        var account = await SeedAccountAsync(db, id: 10, code: "560101", name: "运输成本", balanceDirection: "借");
        await SeedVoucherEntryAsync(db, voucherId: 21, entryId: 31, account, outletCode: "OUT-A", debitAmount: 100m);
        await SeedVoucherEntryAsync(db, voucherId: 22, entryId: 32, account, outletCode: "OUT-B", debitAmount: 200m);

        var service = CreateService(db, currentOrgId: 192);

        var result = await service.GetPLItemDetailAsync(new AmoebaPLItemDetailRequest
        {
            TemplateId = 1,
            AccountSetId = 1,
            PLItemId = 100,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 31),
        });

        Assert.Equal(100m, result.TotalAmount);
        var accountSummary = Assert.Single(result.Accounts);
        var entry = Assert.Single(accountSummary.Entries);
        Assert.Equal(31, entry.EntryId);
    }

    [Fact]
    public async Task GetDepreciationDrillDown_filters_by_current_org_and_pl_item_account_code()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetDepreciationDrillDown_filters_by_current_org_and_pl_item_account_code), orgId: 192);
        await SeedTemplateAndItemAsync(db, plItemId: 100, relatedAccountsJson: """[{"code":"560102"}]""", systemDataSource: "depreciation");

        db.Set<FinAssetCard>().AddRange(
            CreateAsset(id: 1, orgId: 192, accountCode: "560102", name: "匹配资产"),
            CreateAsset(id: 2, orgId: 192, accountCode: "560103", name: "其他科目资产"),
            CreateAsset(id: 3, orgId: 193, accountCode: "560102", name: "其他组织资产")
        );
        await db.SaveChangesAsync();

        var service = CreateService(db, currentOrgId: 192);

        var result = await service.GetDepreciationDrillDownAsync(100, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), accountSetId: 1);

        var asset = Assert.Single(result.Assets);
        Assert.Equal("匹配资产", asset.AssetName);
        Assert.Equal(asset.PeriodDepreciation, result.TotalAmount);
    }

    [Fact]
    public async Task Match_skips_voucher_item_without_related_accounts()
    {
        await using var db = TestDbContextFactory.Create(nameof(Match_skips_voucher_item_without_related_accounts), orgId: 192);
        var service = CreateService(db, currentOrgId: 192);

        // 排序在前、但忘配科目的凭证项：不得吞掉整个数据池
        var emptyItem = CreatePLItem(id: 1, name: "漏配科目项", sort: 1, relatedAccountsJson: null);
        var costItem = CreatePLItem(id: 2, name: "运输成本", sort: 2, relatedAccountsJson: """[{"code":"5601"}]""");
        var points = new List<DataPoint>
        {
            new() { Source = "voucher", AccountCode = "560101", Amount = 100m, Category = "expense" },
            new() { Source = "voucher", AccountCode = "540101", Amount = 50m, Category = "cost" },
        };

        var (matched, unmatched) = service.MatchDataPointsToPLItems(points, new List<FinAmoebaPLItem> { emptyItem, costItem });

        Assert.False(matched.ContainsKey(1));
        Assert.Equal(100m, matched[2]);
        var leftover = Assert.Single(unmatched);
        Assert.Equal("540101", leftover.AccountCode);
    }

    [Fact]
    public async Task Match_still_honors_exclusive_claim_order_for_configured_items()
    {
        await using var db = TestDbContextFactory.Create(nameof(Match_still_honors_exclusive_claim_order_for_configured_items), orgId: 192);
        var service = CreateService(db, currentOrgId: 192);

        var broadItem = CreatePLItem(id: 1, name: "成本合计", sort: 1, relatedAccountsJson: """[{"code":"5601"}]""");
        var narrowItem = CreatePLItem(id: 2, name: "折旧费", sort: 2, relatedAccountsJson: """[{"code":"560102"}]""");
        var points = new List<DataPoint>
        {
            new() { Source = "voucher", AccountCode = "560102", Amount = 80m, Category = "expense" },
        };

        var (matched, unmatched) = service.MatchDataPointsToPLItems(points, new List<FinAmoebaPLItem> { broadItem, narrowItem });

        // 排序在前的项独占数据点，后续项不再重复计入
        Assert.Equal(80m, matched[1]);
        Assert.False(matched.ContainsKey(2));
        Assert.Empty(unmatched);
    }

    [Fact]
    public void ApplyScopeFilter_filters_by_outlet_direction_project_and_cross_dimension_AND()
    {
        var points = new List<DataPoint>
        {
            new() { Source = "voucher", AccountCode = "540102", SiteCode = "OUT-A", AuxValues = new() { ["business_direction"] = "OUT" }, Amount = 10m },
            new() { Source = "voucher", AccountCode = "540102", SiteCode = "OUT-B", AuxValues = new() { ["business_direction"] = "IN" }, Amount = 20m },
            new() { Source = "voucher", AccountCode = "500101", SiteCode = "OUT-A", AuxValues = new() { ["business_direction"] = "OUT", ["project"] = "裹裹项目" }, Amount = 30m },
        };

        // null scope → 全口径不过滤
        Assert.Equal(3, AmoebaPLService.ApplyScopeFilter(points, null).Count);

        // 网点 OUT-A(第1、3点)
        var byOutlet = AmoebaPLService.ApplyScopeFilter(points, new AmoebaReportScope { Outlets = new() { "OUT-A" } });
        Assert.Equal(2, byOutlet.Count);
        Assert.All(byOutlet, p => Assert.Equal("OUT-A", p.SiteCode));

        // 方向 OUT(第1、3点)
        Assert.Equal(2, AmoebaPLService.ApplyScopeFilter(points, new AmoebaReportScope { Directions = new() { "OUT" } }).Count);

        // 项目=裹裹项目 跨网点,仅命中带 project 的点
        var byProject = AmoebaPLService.ApplyScopeFilter(points, new AmoebaReportScope { Projects = new() { "裹裹项目" } });
        Assert.Equal(30m, Assert.Single(byProject).Amount);

        // 跨维 AND:网点OUT-A ∧ 方向OUT → 第1、3点
        Assert.Equal(2, AmoebaPLService.ApplyScopeFilter(points,
            new AmoebaReportScope { Outlets = new() { "OUT-A" }, Directions = new() { "OUT" } }).Count);

        // 严格剔除:约束方向但点无方向辅助 → 被剔除(子报表不掺无归属点)
        var noDir = new List<DataPoint> { new() { Source = "voucher", AccountCode = "560104", Amount = 5m } };
        Assert.Empty(AmoebaPLService.ApplyScopeFilter(noDir, new AmoebaReportScope { Directions = new() { "OUT" } }));
    }

    [Fact]
    public async Task GetPLItemDetail_excludes_branded_import_revenue_entries()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetPLItemDetail_excludes_branded_import_revenue_entries), orgId: 192);
        await SeedTemplateAndItemAsync(db, plItemId: 100, relatedAccountsJson: """[{"code":"5001"}]""");
        var revenue = await SeedAccountAsync(db, id: 11, code: "500101", name: "快递收入", balanceDirection: "贷");
        await SeedVoucherEntryAsync(db, voucherId: 41, entryId: 51, revenue, outletCode: "OUT-A", debitAmount: 0m, creditAmount: 100m);
        await SeedVoucherEntryAsync(db, voucherId: 42, entryId: 52, revenue, outletCode: "OUT-A", debitAmount: 0m, creditAmount: 200m, voucherSource: "极兔导入");

        var service = CreateService(db, currentOrgId: 192);

        var result = await service.GetPLItemDetailAsync(new AmoebaPLItemDetailRequest
        {
            TemplateId = 1,
            AccountSetId = 1,
            PLItemId = 100,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 31),
        });

        // 与主报表同口径：品牌导入凭证的收入分录不入下钻
        Assert.Equal(100m, result.TotalAmount);
        var entry = Assert.Single(Assert.Single(result.Accounts).Entries);
        Assert.Equal(51, entry.EntryId);
    }

    [Fact]
    public async Task GetPLItemDetail_honors_exclusive_claim_by_earlier_item()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetPLItemDetail_honors_exclusive_claim_by_earlier_item), orgId: 192);
        // 排序在前的"成本合计"(5601) 与排序在后的"折旧费"(560102) 前缀重叠
        await SeedTemplateAndItemAsync(db, plItemId: 100, relatedAccountsJson: """[{"code":"5601"}]""");
        db.Set<FinAmoebaPLItem>().Add(new FinAmoebaPLItem
        {
            FID = 101,
            FTemplateId = 1,
            FItemName = "折旧费",
            FNodeRole = "data",
            FSort = 20,
            FRelatedAccountsJson = """[{"code":"560102"}]""",
            F值来源 = "system",
            F系统数据源 = "voucher",
            F项目类别 = "cost",
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var account = await SeedAccountAsync(db, id: 12, code: "560102", name: "折旧", balanceDirection: "借");
        await SeedVoucherEntryAsync(db, voucherId: 43, entryId: 53, account, outletCode: "OUT-A", debitAmount: 80m);

        var service = CreateService(db, currentOrgId: 192);
        var requestFor = (long plItemId) => new AmoebaPLItemDetailRequest
        {
            TemplateId = 1,
            AccountSetId = 1,
            PLItemId = plItemId,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 31),
        };

        // 分录被排序在前的项独占，排序在后的项下钻为空——与报表显示一致
        var earlier = await service.GetPLItemDetailAsync(requestFor(100));
        var later = await service.GetPLItemDetailAsync(requestFor(101));

        Assert.Equal(80m, earlier.TotalAmount);
        Assert.Empty(later.Accounts);
        Assert.Equal(0m, later.TotalAmount);
    }

    [Fact]
    public async Task GetPLItemDetail_no_longer_matches_by_summary_keywords()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetPLItemDetail_no_longer_matches_by_summary_keywords), orgId: 192);
        await SeedTemplateAndItemAsync(db, plItemId: 100, relatedAccountsJson: """[{"code":"5601"}]""");
        var item = await db.Set<FinAmoebaPLItem>().SingleAsync(i => i.FID == 100);
        item.FSummaryKeywordsJson = """["保险"]""";
        await db.SaveChangesAsync();
        // 科目 5401 不在关联科目内，摘要含关键词"保险"——主报表金额匹配不认关键词，下钻同样不应认
        var account = await SeedAccountAsync(db, id: 13, code: "540101", name: "运输成本", balanceDirection: "借");
        await SeedVoucherEntryAsync(db, voucherId: 44, entryId: 54, account, outletCode: "OUT-A", debitAmount: 60m, summary: "保险费支出");

        var service = CreateService(db, currentOrgId: 192);

        var result = await service.GetPLItemDetailAsync(new AmoebaPLItemDetailRequest
        {
            TemplateId = 1,
            AccountSetId = 1,
            PLItemId = 100,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 31),
        });

        Assert.Empty(result.Accounts);
        Assert.Equal(0m, result.TotalAmount);
    }

    [Fact]
    public async Task Match_allows_depreciation_item_without_accounts_to_take_own_source_pool()
    {
        await using var db = TestDbContextFactory.Create(nameof(Match_allows_depreciation_item_without_accounts_to_take_own_source_pool), orgId: 192);
        var service = CreateService(db, currentOrgId: 192);

        // 旧约定：单一无科目折旧项全取折旧池（与折旧下钻语义一致）；凭证池不受其影响
        var depreciationItem = CreatePLItem(id: 1, name: "折旧", sort: 1, relatedAccountsJson: null);
        depreciationItem.F系统数据源 = "depreciation";
        var costItem = CreatePLItem(id: 2, name: "运输成本", sort: 2, relatedAccountsJson: """[{"code":"5601"}]""");
        var points = new List<DataPoint>
        {
            new() { Source = "depreciation", AccountCode = "560102", Amount = 30m, Category = "depreciation" },
            new() { Source = "voucher", AccountCode = "560101", Amount = 100m, Category = "expense" },
        };

        var (matched, unmatched) = service.MatchDataPointsToPLItems(points, new List<FinAmoebaPLItem> { depreciationItem, costItem });

        Assert.Equal(30m, matched[1]);
        Assert.Equal(100m, matched[2]);
        Assert.Empty(unmatched);
    }

    [Fact]
    public async Task GetPLItemDetail_uses_preorder_not_global_fsort_for_claim_order()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetPLItemDetail_uses_preorder_not_global_fsort_for_claim_order), orgId: 192);
        db.Set<FinAmoebaPLTemplate>().Add(new FinAmoebaPLTemplate
        {
            FID = 1,
            FName = "经营报表",
            FAccountSetId = 1,
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        });
        // 跨分区前缀重叠：广义"成本合计"(5601) 在 Tab A，狭义"折旧费"(560102) 在 Tab B。
        // 前序(文档顺序)里 5601 在前；但兄弟内 FSort 倒置(5 vs 1)，全局 OrderBy(FSort)
        // 会让 560102 抢在 5601 前面——这正是下钻曾经与主报表口径漂移的场景。
        db.Set<FinAmoebaPLItem>().AddRange(
            new FinAmoebaPLItem { FID = 10, FTemplateId = 1, FItemName = "成本分区", FNodeRole = "group", FParentId = 0, FSort = 1, FCreatedTime = DateTime.UtcNow, FUpdatedTime = DateTime.UtcNow },
            new FinAmoebaPLItem { FID = 100, FTemplateId = 1, FItemName = "成本合计", FNodeRole = "data", FParentId = 10, FSort = 5, FRelatedAccountsJson = """[{"code":"5601"}]""", F值来源 = "system", F系统数据源 = "voucher", F项目类别 = "cost", FCreatedTime = DateTime.UtcNow, FUpdatedTime = DateTime.UtcNow },
            new FinAmoebaPLItem { FID = 20, FTemplateId = 1, FItemName = "折旧分区", FNodeRole = "group", FParentId = 0, FSort = 2, FCreatedTime = DateTime.UtcNow, FUpdatedTime = DateTime.UtcNow },
            new FinAmoebaPLItem { FID = 101, FTemplateId = 1, FItemName = "折旧费", FNodeRole = "data", FParentId = 20, FSort = 1, FRelatedAccountsJson = """[{"code":"560102"}]""", F值来源 = "system", F系统数据源 = "voucher", F项目类别 = "cost", FCreatedTime = DateTime.UtcNow, FUpdatedTime = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var account = await SeedAccountAsync(db, id: 12, code: "560102", name: "折旧", balanceDirection: "借");
        await SeedVoucherEntryAsync(db, voucherId: 43, entryId: 53, account, outletCode: "OUT-A", debitAmount: 80m);

        var service = CreateService(db, currentOrgId: 192);
        var requestFor = (long plItemId) => new AmoebaPLItemDetailRequest
        {
            TemplateId = 1,
            AccountSetId = 1,
            PLItemId = plItemId,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 31),
        };

        // 主报表(前序匹配)把分录判给排在前序更前的 5601；下钻须一致
        var allItems = await db.Set<FinAmoebaPLItem>().ToListAsync();
        var reportPoints = new List<DataPoint>
        {
            new() { Source = "voucher", AccountCode = "560102", Amount = 80m, Category = "cost", VoucherEntryId = 53 },
        };
        var (matched, _) = service.MatchDataPointsToPLItems(reportPoints, allItems);
        Assert.Equal(80m, matched[100]);
        Assert.False(matched.ContainsKey(101));

        // 下钻与主报表一致：5601 拿到该分录，560102 为空
        var broad = await service.GetPLItemDetailAsync(requestFor(100));
        var narrow = await service.GetPLItemDetailAsync(requestFor(101));

        Assert.Equal(80m, broad.TotalAmount);
        Assert.Empty(narrow.Accounts);
        Assert.Equal(0m, narrow.TotalAmount);
    }

    [Fact]
    public async Task DepreciationDrillDown_total_matches_report_aggregation_for_multiple_cards()
    {
        await using var db = TestDbContextFactory.Create(nameof(DepreciationDrillDown_total_matches_report_aggregation_for_multiple_cards), orgId: 192);
        await SeedTemplateAndItemAsync(db, plItemId: 100, relatedAccountsJson: """[{"code":"560102"}]""", systemDataSource: "depreciation");

        // 三张卡片各计提 100/3=33.3333…：逐卡取整 33.33×3=99.99；若聚合端不取整
        // 则求和为 99.999…，与下钻 99.99 差一分——这正是下钻合计对不上报表的根源。
        db.Set<FinAssetCard>().AddRange(
            CreateAsset(id: 1, orgId: 192, accountCode: "560102", name: "卡片一", originalValue: 100m, usefulLife: 3),
            CreateAsset(id: 2, orgId: 192, accountCode: "560102", name: "卡片二", originalValue: 100m, usefulLife: 3),
            CreateAsset(id: 3, orgId: 192, accountCode: "560102", name: "卡片三", originalValue: 100m, usefulLife: 3)
        );
        await db.SaveChangesAsync();

        var service = CreateService(db, currentOrgId: 192);
        var start = new DateTime(2026, 3, 1);
        var end = new DateTime(2026, 3, 31);

        var reportPoints = await service.AggregateDepreciation(start, end, 192, accountSetId: 1);
        var drill = await service.GetDepreciationDrillDownAsync(100, start, end, accountSetId: 1);

        // 报表口径合计与下钻合计须逐分一致
        Assert.Equal(reportPoints.Sum(d => d.Amount), drill.TotalAmount);
    }

    [Fact]
    public async Task ApplyCommonCostAllocation_allocates_common_cost_by_send_volume_share()
    {
        await using var db = TestDbContextFactory.Create(nameof(ApplyCommonCostAllocation_allocates_common_cost_by_send_volume_share), orgId: 192);
        var service = CreateService(db, currentOrgId: 192);

        // 公共费叶：房租(科目560104, 按发件量分摊)
        var rent = CreatePLItem(id: 601, name: "房租", sort: 1, relatedAccountsJson: """[{"code":"560104"}]""");
        rent.F分摊方式 = "volume";
        rent.F分摊基数 = "send";
        var plItems = new List<FinAmoebaPLItem> { rent };

        // 全口径基线：OUT-A 发件300 + OUT-B 发件200 = 500；房租公司全额 1000(CMB 凭证，无网点)
        var fullPoints = new List<DataPoint>
        {
            new() { Source = "billing", SiteCode = "OUT-A", WaybillCount = 300, Amount = 3000m, IsPriced = true },
            new() { Source = "billing", SiteCode = "OUT-B", WaybillCount = 200, Amount = 2000m, IsPriced = true },
            new() { Source = "voucher", AccountCode = "560104", Amount = 1000m, Category = "expense", AuxValues = new() { ["business_direction"] = "CMB" } },
        };
        // 子报表 scope=OUT-A：房租 CMB 凭证被剔除，billing 仅留 OUT-A(发件300)
        var scopedPoints = AmoebaPLService.ApplyScopeFilter(fullPoints, new AmoebaReportScope { Outlets = new() { "OUT-A" } });
        var (scopedMatched, _) = service.MatchDataPointsToPLItems(scopedPoints, plItems);
        var warnings = new List<string>();

        // send 基数叶，deliver 件量不参与该叶分摊，传 0/0
        service.ApplyCommonCostAllocation(scopedMatched, scopedPoints, fullPoints, plItems,
            indicatorItems: new List<FinAmoebaPLItem>(), allItems: plItems, warnings,
            scopedDeliver: 0L, fullDeliver: 0L);

        Assert.Equal(600m, scopedMatched[601]);   // 1000 × 300/500
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task ApplyCommonCostAllocation_removes_direct_residual_when_full_total_absent()
    {
        await using var db = TestDbContextFactory.Create(nameof(ApplyCommonCostAllocation_removes_direct_residual_when_full_total_absent), orgId: 192);
        var service = CreateService(db, currentOrgId: 192);

        // 出港操作费(科目540104, send 分摊)——出港方向随 scope 存活，直接匹配会出残值
        var opFee = CreatePLItem(id: 610, name: "出港操作费", sort: 1, relatedAccountsJson: """[{"code":"540104"}]""");
        opFee.F分摊方式 = "volume";
        opFee.F分摊基数 = "send";
        var plItems = new List<FinAmoebaPLItem> { opFee };

        var scopedPoints = new List<DataPoint>
        {
            new() { Source = "billing", SiteCode = "OUT-A", WaybillCount = 100, Amount = 1000m, IsPriced = true },
            new() { Source = "voucher", AccountCode = "540104", SiteCode = "OUT-A", Amount = 50m, Category = "cost", AuxValues = new() { ["business_direction"] = "OUT" } },
        };
        // 全口径基线无 540104 发生额 → 公共费叶全额缺失
        var fullPoints = new List<DataPoint>
        {
            new() { Source = "billing", SiteCode = "OUT-A", WaybillCount = 100, Amount = 1000m, IsPriced = true },
        };
        var (scopedMatched, _) = service.MatchDataPointsToPLItems(scopedPoints, plItems);
        Assert.Equal(50m, scopedMatched[610]);   // 前置：直接匹配确有残值

        service.ApplyCommonCostAllocation(scopedMatched, scopedPoints, fullPoints, plItems,
            indicatorItems: new List<FinAmoebaPLItem>(), allItems: plItems, warnings: new List<string>(),
            scopedDeliver: 0L, fullDeliver: 0L);

        // 子报表公共费叶不得留直接归段残值：全额缺失 → 移除(既不分摊也不直接归段)
        Assert.False(scopedMatched.ContainsKey(610));
    }

    [Fact]
    public async Task SaveEstimateData_writes_period_key_with_month_prefix()
    {
        await using var db = TestDbContextFactory.Create(nameof(SaveEstimateData_writes_period_key_with_month_prefix), orgId: 192);
        var service = CreateService(db, currentOrgId: 192);

        await service.SaveEstimateDataAsync(new ManualDataDto
        {
            TemplateId = 1,
            OrgId = 192,
            Period = "202603",
            PLItemId = 100,
            Amount = 500m,
            AccountCode = "560104",
            AuxiliaryJson = "[]",
        });

        var saved = await db.Set<FinAmoebaManualData>().SingleAsync();
        Assert.Equal("M:202603", saved.FPeriodKey);   // 估值录入 UI 当前为月度，期间键 = 'M:'+期间
    }

    [Fact]
    public async Task SaveEstimateData_update_branch_backfills_null_period_key()
    {
        await using var db = TestDbContextFactory.Create(nameof(SaveEstimateData_update_branch_backfills_null_period_key), orgId: 192);
        // 模拟存量/异常路径插入的 FPeriodKey 残留 NULL 的估值行
        db.Set<FinAmoebaManualData>().Add(new FinAmoebaManualData
        {
            FTemplateId = 1, FOrgId = 192, FPeriod = "202603", FPeriodKey = null,
            FDataType = "estimate", FAccountCode = "560104", FAuxiliaryJson = "[]",
            FAmount = 100m, FCreatedTime = DateTime.Now, FUpdatedTime = DateTime.Now,
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, currentOrgId: 192);

        // UPSERT 命中 update 分支（同 Template/Org/Period/AccountCode/AuxJson）
        await service.SaveEstimateDataAsync(new ManualDataDto
        {
            TemplateId = 1, OrgId = 192, Period = "202603",
            Amount = 200m, AccountCode = "560104", AuxiliaryJson = "[]",
        });

        var saved = await db.Set<FinAmoebaManualData>().SingleAsync();
        Assert.Equal(200m, saved.FAmount);            // 确为 update（非新增第二行）
        Assert.Equal("M:202603", saved.FPeriodKey);   // 自愈：NULL 期间键被回填
    }

    private static FinAmoebaPLItem CreatePLItem(long id, string name, int sort, string? relatedAccountsJson)
    {
        return new FinAmoebaPLItem
        {
            FID = id,
            FTemplateId = 1,
            FItemName = name,
            FNodeRole = "data",
            FSort = sort,
            FRelatedAccountsJson = relatedAccountsJson,
            F值来源 = "system",
            F系统数据源 = "voucher",
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        };
    }

    private static async Task SeedTemplateAndItemAsync(
        STOTOPDbContext db,
        long plItemId,
        string relatedAccountsJson,
        string systemDataSource = "voucher")
    {
        db.Set<FinAmoebaPLTemplate>().Add(new FinAmoebaPLTemplate
        {
            FID = 1,
            FName = "经营报表",
            FAccountSetId = 1,
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        });
        db.Set<FinAmoebaPLItem>().Add(new FinAmoebaPLItem
        {
            FID = plItemId,
            FTemplateId = 1,
            FItemName = "成本项",
            FNodeRole = "data",
            FSort = 10,
            FRelatedAccountsJson = relatedAccountsJson,
            F值来源 = "system",
            F系统数据源 = systemDataSource,
            F项目类别 = "cost",
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<FinAccount> SeedAccountAsync(STOTOPDbContext db, long id, string code, string name, string balanceDirection)
    {
        var account = new FinAccount
        {
            FID = id,
            FCode = code,
            FName = name,
            FBalanceDirection = balanceDirection,
            FAccountSetId = 1,
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        };
        db.Set<FinAccount>().Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    private static async Task SeedVoucherEntryAsync(
        STOTOPDbContext db,
        long voucherId,
        long entryId,
        FinAccount account,
        string outletCode,
        decimal debitAmount,
        decimal creditAmount = 0,
        string? voucherSource = null,
        string summary = "测试分录")
    {
        db.Set<FinVoucher>().Add(new FinVoucher
        {
            FID = voucherId,
            FVoucherWord = "记",
            FVoucherNo = (int)voucherId,
            FDate = new DateTime(2026, 3, 10),
            FStatus = 1,
            FAccountSetId = 1,
            FOrgId = 192,
            FSource = voucherSource,
            FCreator = "test",
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        });
        db.Set<FinVoucherEntry>().Add(new FinVoucherEntry
        {
            FID = entryId,
            FVoucherId = voucherId,
            FLineNo = 1,
            FSummary = summary,
            FAccountId = account.FID,
            FAccountCode = account.FCode,
            FAccountName = account.FName,
            FAuxiliaryJson = $$"""[{"type":"outlet","id":0,"code":"{{outletCode}}","name":"{{outletCode}}"}]""",
            FDebitAmount = debitAmount,
            FCreditAmount = creditAmount,
            FOrgId = 192,
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static FinAssetCard CreateAsset(long id, long orgId, string accountCode, string name, decimal originalValue = 1200m, int usefulLife = 12)
    {
        return new FinAssetCard
        {
            FID = id,
            FCode = $"A{id}",
            FName = name,
            FOriginalValue = originalValue,
            FUsefulLife = usefulLife,
            FResidualRate = 0,
            FStatus = 1,
            FOrgId = orgId,
            FAccountSetId = 1,
            FRemark = $"借:{accountCode}/部门测试 | 贷:1602/部门测试",
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        };
    }

    private static AmoebaPLService CreateService(STOTOPDbContext db, long currentOrgId)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        httpContextAccessor.HttpContext.Items["CurrentOrgId"] = currentOrgId;

        return new AmoebaPLService(
            db,
            new Repository<FinVoucherEntry>(db),
            new Repository<FinVoucher>(db),
            new Repository<FinAccount>(db),
            httpContextAccessor,
            new FormulaEngineImpl(),
            new CommonCostAllocationEngine());
    }
}
