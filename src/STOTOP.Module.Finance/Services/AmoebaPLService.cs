using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using NPOI.XSSF.UserModel.Extensions;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Constants;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.System.Entities;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

using STOTOP.Module.Finance.Services.FormulaEngine;

namespace STOTOP.Module.Finance.Services;

public class AmoebaPLService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly IRepository<FinVoucherEntry> _voucherEntryRepository;
    private readonly IRepository<FinVoucher> _voucherRepository;
    private readonly IRepository<FinAccount> _accountRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IFormulaEngine _formulaEngine;
    private readonly ICommonCostAllocationEngine _allocEngine;

    // 品牌来源关键词（用于排除收入类凭证去重）
    private static readonly string[] ExcludedRevenueVoucherSources = { "极兔导入", "韵达导入", "申通导入", "计费生成" };

    public AmoebaPLService(
        STOTOPDbContext dbContext,
        IRepository<FinVoucherEntry> voucherEntryRepository,
        IRepository<FinVoucher> voucherRepository,
        IRepository<FinAccount> accountRepository,
        IHttpContextAccessor httpContextAccessor,
        IFormulaEngine formulaEngine,
        ICommonCostAllocationEngine allocEngine)
    {
        _dbContext = dbContext;
        _voucherEntryRepository = voucherEntryRepository;
        _voucherRepository = voucherRepository;
        _accountRepository = accountRepository;
        _httpContextAccessor = httpContextAccessor;
        _formulaEngine = formulaEngine;
        _allocEngine = allocEngine;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    #region 主入口

    /// <summary>
    /// 获取阿米巴经营报表
    /// </summary>
    public async Task<AmoebaReportResponse> GetReportAsync(AmoebaReportRequest request)
    {
        var orgId = request.OrgId > 0 ? request.OrgId : GetCurrentOrgId();
        var accountSetId = request.AccountSetId;

        // 1. 加载映射规则
        var mappingRules = await _dbContext.Set<FinAmoebaMappingRule>()
            .Where(r => r.FOrgId == orgId)
            .OrderByDescending(r => r.FPriority)
            .ToListAsync();

        // 2. 加载损益模板和损益项
        long templateId = request.TemplateId;
        if (templateId == 0 && accountSetId > 0)
        {
            var tpl = await _dbContext.Set<FinAmoebaPLTemplate>()
                .FirstOrDefaultAsync(t => t.FAccountSetId == accountSetId);
            templateId = tpl?.FID ?? 0;
        }
        // 模板无法解析时使用空列表：决不能回落到全库损益项（跨模板/账套/组织串数据）
        var plItems = templateId > 0
            ? await _dbContext.Set<FinAmoebaPLItem>().Where(i => i.FTemplateId == templateId).ToListAsync()
            : new List<FinAmoebaPLItem>();

        // 3. 加载手工分类
        var manualClassifications = await _dbContext.Set<FinAmoebaManualClassify>()
            .Where(m => m.FOrgId == orgId)
            .ToListAsync();

        // 5. 加载经营单元（从辅助核算项目 business_unit 类型获取）
        var units = await _dbContext.Set<FinAuxiliaryItem>()
            .Where(u => u.FOrgId == orgId && u.FAuxType == "business_unit" && u.FEnableStatus == 1)
            .ToListAsync();

        // 6. 顺序执行避免 DbContext 并发冲突
        var billingData = await AggregateBillingRevenue(request.StartDate, request.EndDate, orgId, plItems, mappingRules, request.UnitIds, request.SiteCodes);
        var voucherData = await AggregateVoucherData(request.StartDate, request.EndDate, orgId, accountSetId);
        var depreciationData = await AggregateDepreciation(request.StartDate, request.EndDate, orgId, accountSetId);

        var allDataPoints = new List<DataPoint>();
        allDataPoints.AddRange(billingData);
        allDataPoints.AddRange(voucherData);
        allDataPoints.AddRange(depreciationData);

        // 7. 方向分类
        foreach (var point in allDataPoints)
        {
            if (string.IsNullOrEmpty(point.Direction))
            {
                point.Direction = ClassifyDirection(point, plItems, manualClassifications);
            }
        }

        // 8. 映射到经营单元
        foreach (var point in allDataPoints)
        {
            point.MappedUnitId = MapToUnit(point, mappingRules);
        }

        // [批次5-S6] 旧固定比例分摊(FinAmoebaAllocation)已废弃删除；数据点直接进入视图(件量分摊在多期路径)
        var allocatedPoints = allDataPoints;

        // 10. 按方向筛选（按归并桶比较，兼容模板 Tab 自定义命名）
        if (!string.IsNullOrEmpty(request.Direction))
        {
            allocatedPoints = allocatedPoints
                .Where(p => MapDirectionToBucket(p.Direction) == request.Direction)
                .ToList();
        }

        // 11. 按品牌筛选（严格口径：无品牌归属的数据点同样剔除，避免单品牌视图掺入全部公共费用）
        if (request.BrandCodes != null && request.BrandCodes.Count > 0)
        {
            allocatedPoints = allocatedPoints
                .Where(p => !string.IsNullOrEmpty(p.BrandCode) && request.BrandCodes.Contains(p.BrandCode))
                .ToList();
        }

        // 12. 构建响应
        AmoebaReportResponse response;
        if (request.ViewMode == "site")
        {
            response = BuildSiteViewResponse(allocatedPoints, units, request);
        }
        else
        {
            response = BuildUnitViewResponse(allocatedPoints, units, request);
        }

        // 13. 组织级汇总
        response.OrgSummary = BuildOrgSummary(allocatedPoints, orgId, request);

        // 14. 未分类统计
        var unclassified = allocatedPoints.Where(p => p.MappedUnitId == null).ToList();
        response.UnclassifiedAmount = unclassified.Sum(p => p.Amount);
        response.UnclassifiedCount = unclassified.Count;

        return response;
    }

    /// <summary>
    /// 多期对比阿米巴报表（功能分区制）
    /// 按模板树结构组装，支持环比/同比对比
    /// </summary>
    public async Task<AmoebaMultiPeriodResponse> GetMultiPeriodReportAsync(AmoebaMultiPeriodRequest request)
    {
        // 安全(P1-4)：报表组织一律取当前登录组织，忽略请求体 OrgId，避免越权读他组织数据。
        // 凭证源已由 IOrgScoped 全局过滤按当前组织过滤，此处统一 billing/depreciation/estimate 其余三源口径，
        // 杜绝"传 OrgId=B + AccountSetId=A 把两组织数据拼一张表"。如需授权用户跨组织出报表，
        // 改为校验 request.OrgId ∈ 用户可访问组织集后再用 request.OrgId。
        var orgId = GetCurrentOrgId();
        var accountSetId = request.AccountSetId;

        // 0. 入参校验：粒度 + 期间格式（畸形输入会让后续 Parse 直接 500）与模板存在性/账套归属
        var granularity = NormalizeGranularity(request.Granularity);   // 非法粒度即抛；null/空→month
        if (!IsValidPeriod(request.MainPeriod, granularity))
            throw new ArgumentException($"期间格式与粒度({granularity})不符，当前值：{request.MainPeriod}");

        // 1. 加载模板实体
        var template = await _dbContext.Set<FinAmoebaPLTemplate>()
            .FirstOrDefaultAsync(t => t.FID == request.TemplateId);
        if (template == null)
            throw new InvalidOperationException("损益模板不存在");
        if (accountSetId > 0 && template.FAccountSetId != accountSetId)
            throw new InvalidOperationException("损益模板不属于当前账套，请重新选择模板");

        // 2. 加载模板树（按 FSort 升序）
        var plItems = await _dbContext.Set<FinAmoebaPLItem>()
            .Where(i => i.FTemplateId == request.TemplateId)
            .OrderBy(i => i.FSort)
            .ToListAsync();

        // 3. 识别 Tab 节点（FParentId==0 且 FNodeRole=="group"）和全局汇总行（FParentId==0 且 FNodeRole=="formula"）
        var tabRoots = plItems
            .Where(i => i.FParentId == 0 && i.FNodeRole == "group")
            .OrderBy(i => i.FSort)
            .ToList();
        var globalFormulaRoots = plItems
            .Where(i => i.FParentId == 0 && i.FNodeRole == "formula")
            .OrderBy(i => i.FSort)
            .ToList();

        // 3.1 识别全局指标分区（根级且 F是否指标分区==true）并从主列表分离。
        // 指标分区按设计为根级节点（参见 FinAmoebaPLItem.F是否指标分区 注释与 FinanceSeeder.MigrateV4）；
        // 历史脏数据中非根级的标记不作分离，保持其在 Tab 内按普通分组展示
        var allItems = plItems.ToList(); // 工作副本
        var indicatorSectionRoots = allItems
            .Where(i => i.F是否指标分区 && i.FParentId == 0)
            .OrderBy(i => i.FSort)
            .ToList();
        List<FinAmoebaPLItem> indicatorItems = new();
        if (indicatorSectionRoots.Count > 0)
        {
            // 收集各 indicator section 下的所有子项
            foreach (var sectionRoot in indicatorSectionRoots)
            {
                indicatorItems.AddRange(allItems.Where(i => IsDescendantOf(i, sectionRoot.FID, allItems)));
                indicatorItems.Add(sectionRoot);
            }
            // 从主列表中移除，不参与 Tab 分组
            var indicatorIds = indicatorItems.Select(i => i.FID).ToHashSet();
            plItems = allItems.Where(i => !indicatorIds.Contains(i.FID)).ToList();
            // 同步更新 tabRoots（指标分区是顶级 group，须从 Tab 列表剔除）
            tabRoots = plItems
                .Where(i => i.FParentId == 0 && i.FNodeRole == "group")
                .OrderBy(i => i.FSort)
                .ToList();
        }

        // 4. 推算期间列表：[当期, 环比期, 同比期(可选)]（按粒度）
        var periods = new List<string> { request.MainPeriod };
        var momPeriod = CalcPreviousPeriod(request.MainPeriod, granularity);
        periods.Add(momPeriod);
        string? yoyPeriod = null;
        if (request.IncludeYoy)
        {
            yoyPeriod = CalcYoyPeriod(request.MainPeriod, granularity);
            if (yoyPeriod != null) periods.Add(yoyPeriod);   // 日/周粒度无同比，CalcYoyPeriod 返回 null
        }

        // 5. 加载映射规则（仅供下游调度，不再作为报表主流程的全局筛选依据）
        // 指令要求："移除全局筛选"后，映射规则仅用于计费聚合内部需要。
        var mappingRules = await _dbContext.Set<FinAmoebaMappingRule>()
            .Where(r => r.FOrgId == orgId)
            .OrderByDescending(r => r.FPriority)
            .ToListAsync();

        // 6. 顺序聚合各期间数据（避免 DbContext 并发冲突）
        // 全局品牌/网点/经营单元筛选已移除—— 所有数据点交由 公式驱动的 PLItem 独占匹配职责。
        var periodResults = new List<List<DataPoint>>();
        // [批次5-S2] 子报表的同期全口径基线(scope 过滤前全集)，供公共费分摊取全额/全口径件量；全口径请求为 null
        var periodFullScopePoints = new List<List<DataPoint>?>();
        bool isSubReport = request.Scope?.IsSubReport == true;
        // [批次5-S3] 各期的期间键(粒度前缀+期间)：manual 加载/去重与估算取数按此键
        var periodKeys = periods.Select(p => BuildPeriodKey(p, granularity)).ToList();
        for (int i = 0; i < periods.Count; i++)
        {
            // [批次5-S3] 按粒度选源(C1 互斥)：台账月/季/年=billing+voucher+depreciation；估算日/周=billing+estimate
            var all = await BuildPeriodDataPoints(periods[i], granularity, orgId, accountSetId, request.TemplateId, plItems, mappingRules);
            // [批次5-S2] scope 过滤前留存全口径基线：聚合无 SQL 级 scope，ApplyScopeFilter 返回新列表不动 all，
            // 故 all 即免费的全口径基线(纠正设计 §4.5 关于额外 DB 聚合的悲观假设)
            periodFullScopePoints.Add(isSubReport ? all : null);
            // [方案B 批次4] L1 请求级 scope 预过滤(独占匹配之前)：网点/项目/方向等;Scope=null 则全口径不变
            all = ApplyScopeFilter(all, request.Scope);
            periodResults.Add(all);
        }

        // 6. 加载手工填报数据（覆盖所有相关期间，按期间键 [批次5-S3]）
        // 仅取 manual 类型：estimate 数据 FPLItemId 可为 null，且会在 (FPeriodKey,FPLItemId) ToDictionary 时
        // 因重复键抛 ArgumentException 导致 500；estimate 数据已由 AggregateEstimateData 单独走独占匹配。
        var manualData = await _dbContext.Set<FinAmoebaManualData>()
            .Where(m => m.FTemplateId == request.TemplateId
                        && m.FOrgId == orgId
                        && m.FDataType == "manual"
                        && periodKeys.Contains(m.FPeriodKey))
            .ToListAsync();

        // 7. 对每个期间执行独占匹配，得到 PLItemId -> Amount 字典
        var unmatchedWarnings = new List<string>();
        var perPeriodAmounts = new List<Dictionary<long, decimal>>();
        // [批次5-S4] 日/周公共费分摊基线缓存：所属当月数据点(实际凭证/估值，含当月例外)，同月一次请求内只聚合一次
        var monthPointsCache = new Dictionary<string, List<DataPoint>>();
        // [派件量接入 §B2 审查 I1] 日/周分母 deliver 按月缓存：fullDeliver 是所属当月全网点件量，同月多日/多周
        //   会发出多次完全相同的全月聚合 SQL(N+1)，按月缓存使每月只聚合一次(镜像 monthPointsCache)。
        var fullDeliverCache = new Dictionary<string, long>();
        async Task<List<DataPoint>> GetMonthPoints(string month)
        {
            if (!monthPointsCache.TryGetValue(month, out var pts))
            {
                pts = await BuildPeriodDataPoints(month, "month", orgId, accountSetId, request.TemplateId, plItems, mappingRules);
                monthPointsCache[month] = pts;
            }
            return pts;
        }
        for (int i = 0; i < periods.Count; i++)
        {
            var (matched, unmatched) = MatchDataPointsToPLItems(periodResults[i], plItems);
            perPeriodAmounts.Add(matched);
            if (i == 0)
            {
                // billing 数据点匹配后不会从池中移除（支持金额/件量/重量多聚合复用），
                // 统计未匹配时须排除，否则已匹配的计费数据会让警告条数与金额双重虚高
                var unmatchedAccountBased = unmatched
                    .Where(p => p.Source == "voucher" || p.Source == "depreciation" || p.Source == "estimate")
                    .ToList();
                if (unmatchedAccountBased.Count > 0)
                {
                    var totalUnmatched = unmatchedAccountBased.Sum(p => p.Amount);
                    unmatchedWarnings.Add($"当期存在 {unmatchedAccountBased.Count} 条未匹配数据，金额合计 {totalUnmatched:N2} 元");
                }
            }
            // 指标分区独立匹配：指标项从完整数据池中取值（与损益项不互斥）
            if (indicatorItems.Count > 0)
            {
                var (indMatched, _) = MatchDataPointsToPLItems(periodResults[i], indicatorItems);
                foreach (var kv in indMatched)
                    matched[kv.Key] = kv.Value;
            }

            // 公共费按件量分摊注入(覆盖公共费叶=总额×分子件量/分母件量)，置于指标并入之后(ComputeVolumeBasis 需读票量 indicator)。
            // [批次5-S4] 日/周：基线=所属当月实际(月公共费×日周件量/当月件量 线性时间摊；子报表则分子为日周scope件量、
            //   分母为当月全口径件量，时间+scope 一次比例)。[批次5-S2] 月/季/年子报表：基线=同期全口径。
            if (!IsLedgerGranularity(granularity))
            {
                var allocWarnings = new List<string>();
                // [批次5-S4] 跨月周告警：周可横跨两月，分子件量含两月而分母只取周首月——比例为近似(审查 medium)
                var (wkStart, wkEnd) = PeriodToDateRange(periods[i], granularity);
                if (wkStart.Month != wkEnd.Month)
                    allocWarnings.Add($"跨月期间公共费按周首月 {PeriodContainingMonth(periods[i], granularity)} 线性摊(分子含另一月天数，比例为近似)");
                var monthPts = await GetMonthPoints(PeriodContainingMonth(periods[i], granularity));
                // [派件量接入 §B2] deliver 件量从 STG申通派件日明细 取：分子=本期(scope 网点)、分母=所属当月(全网点)，
                // 与 monthPts 月公共费全额配对成「时间+scope」一次比例（镜像 send 侧 billing 件量口径）。
                var scopedDeliver = await AggregateDeliverVolume(wkStart, wkEnd, orgId, request.Scope);
                // [审查 I1] fullDeliver 按月缓存：同月多日/多周只对全月聚合一次(scopedDeliver 仍每期算)
                var monthKey = PeriodContainingMonth(periods[i], granularity);
                if (!fullDeliverCache.TryGetValue(monthKey, out var fullDeliver))
                {
                    var (mStart, mEnd) = PeriodToDateRange(monthKey, "month");
                    fullDeliver = await AggregateDeliverVolume(mStart, mEnd, orgId, null);
                    fullDeliverCache[monthKey] = fullDeliver;
                }
                ApplyCommonCostAllocation(matched, periodResults[i], monthPts, plItems, indicatorItems, allItems, allocWarnings, scopedDeliver, fullDeliver);
                // [批次5-S4 审查] 各期均收集告警(原仅 i==0)，使日/周 deliver件量=0/件量=0 在环比同比列也可见
                unmatchedWarnings.AddRange(allocWarnings.Select(w => periods.Count > 1 ? $"[{periods[i]}] {w}" : w));
            }
            else if (isSubReport && periodFullScopePoints[i] is { } fullPts)
            {
                var allocWarnings = new List<string>();
                // [派件量接入 §B2] 月/季/年子报表：deliver 件量同期取——分子=本期 scope 网点、分母=同期全网点，
                // 与 fullPts 同期全口径全额配对成纯 scope 比例（无时间维，故 scoped/full 用同一期间范围）。
                var (pStart, pEnd) = PeriodToDateRange(periods[i], granularity);
                var scopedDeliver = await AggregateDeliverVolume(pStart, pEnd, orgId, request.Scope);
                var fullDeliver = await AggregateDeliverVolume(pStart, pEnd, orgId, null);
                ApplyCommonCostAllocation(matched, periodResults[i], fullPts, plItems, indicatorItems, allItems, allocWarnings, scopedDeliver, fullDeliver);
                unmatchedWarnings.AddRange(allocWarnings.Select(w => periods.Count > 1 ? $"[{periods[i]}] {w}" : w));
            }
        }

        // 手工填报快查：(periodKey, plItemId) -> ManualData [批次5-S3]
        // 防御：理论上 (FTemplateId,FPLItemId,FOrgId,FPeriod) 已建索引应唯一，但若历史数据有重复则保留首条，避免 500
        var manualLookup = manualData
            .GroupBy(m => (m.FPeriodKey ?? "", m.FPLItemId))   // 期间键归一非空，键型保持 (string, long?)
            .ToDictionary(g => g.Key, g => g.First());

        // 8. 单票&均：方向化分母 [批次5-S5]（替代原全局单一 currentTickets）。
        //    出港 tab→发件票量、进港→派件票量、公共/指标→合计件量；按当期取(手工填报优先→独占匹配)，
        //    环比/同比各列独立计算。
        var (sendIndId, deliverIndId) = ResolveVolumeIndicatorIds(allItems);
        decimal TicketValue(long? indicatorId, int p)
        {
            if (indicatorId is not long id) return 0m;
            if (manualLookup.TryGetValue((periodKeys[p], (long?)id), out var md)) return md.FAmount;
            return perPeriodAmounts[p].TryGetValue(id, out var matched) ? matched : 0m;
        }
        decimal? ComputePerUnit(long tabAncestorId, decimal amount, int periodIdx)
            => PerUnitValue(
                TabToDirection(allItems.FirstOrDefault(i => i.FID == tabAncestorId)?.FItemName),
                amount, TicketValue(sendIndId, periodIdx), TicketValue(deliverIndId, periodIdx));
        // 当期(period 0)合计票数：供响应 Summary 展示
        int currentPeriodTickets = (int)(TicketValue(sendIndId, 0) + TicketValue(deliverIndId, 0));

        // 9. 构建 TabAncestorId 映射（每个节点寻找其 depth=0 的 group 祖先）
        var tabAncestorMap = BuildTabAncestorMap(plItems, tabRoots);
        
        // 10. 按 Tab 分组构建子树，组装分区数据
        var sections = new List<SectionData>();
        var sectionRootMap = new List<(SectionData Section, FinAmoebaPLItem Root)>();
        foreach (var root in tabRoots)
        {
            var section = new SectionData
            {
                SectionName = root.FItemName,
                TabAncestorId = root.FID,
            };
            sectionRootMap.Add((section, root));
        
            // 前序遍历该分区下的所有子项
            var traversed = new List<(FinAmoebaPLItem item, int depth)>();
            CollectChildrenPreorder(root, plItems, traversed, 0);
        
            foreach (var (child, depth) in traversed)
            {
                var item = new MultiPeriodPLItemData
                {
                    Id = child.FID,
                    Name = child.FItemName,
                    Unit = child.FUnit,
                    DataSourceRemark = child.FDataSourceRemark,
                    CalculationLogic = child.FCalculationLogic,
                    IsManualEntry = child.FIsManualEntry,
                    NodeRole = child.FNodeRole,
                    Depth = depth,
                    // 下钒判断：优先使用新字段，否则 fallback 到旧字段
                    CanDrillDown = child.F项目类别 != null
                        ? (child.F项目类别 == "revenue" || child.F项目类别 == "cost") && child.F值来源 == "system" && !child.FIsManualEntry
                        : child.FNodeRole == "data" && !child.FIsManualEntry,
                    ItemCategory = child.F项目类别,
                    ValueSource = child.F值来源,
                    DecimalPlaces = child.F小数位数,
                };
        
                // 填充每个期间的值
                for (int p = 0; p < periods.Count; p++)
                {
                    var period = periods[p];
                    var periodKey = periodKeys[p];   // [批次5-S3] manual 查询按期间键
                    decimal amount = 0m;
                    decimal? perUnit = null;

                    if (child.FNodeRole == "group")
                    {
                        // group: SUM_CHILDREN（只累加 data 和 group 子项，排除 formula/indicator）
                        amount = SumChildrenForGroup(child, plItems, perPeriodAmounts[p], manualLookup, periodKey);
                    }
                    else if (child.FNodeRole == "formula")
                    {
                        // formula: 由公式引擎求值，此处先设 0，待后续 EvaluateFormulaItems 处理
                        amount = 0m;
                    }
                    else if (child.FNodeRole == "indicator")
                    {
                        // indicator: 手工填报优先；非手工（系统数据源）读取独占匹配结果；带公式的由公式求值阶段回填
                        if (child.FIsManualEntry && manualLookup.TryGetValue((periodKey, (long?)child.FID), out var mdInd))
                        {
                            amount = mdInd.FAmount;
                            if (child.FPerUnitMode == "manual") perUnit = mdInd.FPerUnitValue;
                        }
                        else if (!child.FIsManualEntry && perPeriodAmounts[p].TryGetValue(child.FID, out var indAmt))
                        {
                            amount = indAmt;
                        }
                    }
                    else if (child.FIsManualEntry)
                    {
                        if (manualLookup.TryGetValue((periodKey, (long?)child.FID), out var md))
                        {
                            amount = md.FAmount;
                            if (child.FPerUnitMode == "manual")
                                perUnit = md.FPerUnitValue;
                        }
                    }
                    else
                    {
                        // data 节点：从独占匹配结果取值
                        if (perPeriodAmounts[p].TryGetValue(child.FID, out var amt))
                            amount = amt;
                    }
        
                    // 单票&均自动计算 [批次5-S5]：方向化分母(随 Tab) + 按当期(含环比/同比)。
                    // formula 项金额此刻尚为 0(待 EvaluateFormulaItems 回填)，跳过避免算出误导性的 0 单票均。
                    if (perUnit == null && child.FPerUnitMode == "auto" && child.FNodeRole != "formula")
                        perUnit = ComputePerUnit(root.FID, amount, p);

                    item.PeriodValues.Add(new PeriodValue
                    {
                        PeriodLabel = period,
                        Amount = amount,
                        PerUnitValue = perUnit,
                    });
                }
        
                section.Items.Add(item);
            }
        
            // 分区初始合计：depth=0 group 不做 SUM_CHILDREN，其值由 FFormula 指定
            // 如有 FFormula 则待后续 Section 公式求值覆盖
            if (!string.IsNullOrWhiteSpace(root.FFormula))
            {
                // 有公式的 depth=0 group，SectionTotals 留待公式求值阶段填充
                section.SectionTotals = null;
            }
            else
            {
                // 无公式的 depth=0 group，值为 null（不做 SUM_CHILDREN）
                section.SectionTotals = null;
            }
        
            sections.Add(section);
        }
        
        // 10.1 构建全局指标分区数据（在公式求值之前，使分区内公式项/带公式指标项参与求值）
        var indicatorSectionData = new List<SectionData>();
        foreach (var sectionRoot in indicatorSectionRoots)
        {
            var indSection = new SectionData
            {
                SectionName = sectionRoot.FItemName,
                TabAncestorId = 0, // 不归属任何 Tab
            };
            var indTraversed = new List<(FinAmoebaPLItem item, int depth)>();
            CollectChildrenPreorder(sectionRoot, allItems, indTraversed, 0);
            foreach (var (child, depth) in indTraversed)
            {
                var indItem = new MultiPeriodPLItemData
                {
                    Id = child.FID,
                    Name = child.FItemName,
                    Unit = child.FUnit,
                    DataSourceRemark = child.FDataSourceRemark,
                    CalculationLogic = child.FCalculationLogic,
                    IsManualEntry = child.FIsManualEntry,
                    NodeRole = child.FNodeRole,
                    Depth = depth,
                    CanDrillDown = false, // indicator 项不支持下钻
                    ItemCategory = child.F项目类别,
                    ValueSource = child.F值来源,
                    DecimalPlaces = child.F小数位数,
                };
                for (int p = 0; p < periods.Count; p++)
                {
                    var period = periods[p];
                    var periodKey = periodKeys[p];   // [批次5-S3] manual 查询按期间键
                    decimal amount = 0m;
                    decimal? perUnit = null;
                    if (child.FIsManualEntry && manualLookup.TryGetValue((periodKey, (long?)child.FID), out var mdInd))
                    {
                        amount = mdInd.FAmount;
                        if (child.FPerUnitMode == "manual") perUnit = mdInd.FPerUnitValue;
                    }
                    else if (!child.FIsManualEntry && child.FNodeRole != "group" && child.FNodeRole != "formula")
                    {
                        if (perPeriodAmounts[p].TryGetValue(child.FID, out var amt))
                            amount = amt;
                    }
                    if (perUnit == null && child.FPerUnitMode == "auto" && child.FNodeRole != "formula")
                        perUnit = ComputePerUnit(sectionRoot.FID, amount, p);   // [批次5-S5] 指标分区按其方向(名称)取分母
                    indItem.PeriodValues.Add(new PeriodValue { PeriodLabel = period, Amount = amount, PerUnitValue = perUnit });
                }
                indSection.Items.Add(indItem);
            }
            indicatorSectionData.Add(indSection);
        }

        // 主 Tab 分区与指标分区的合集：公式求值与环比/同比对两者一视同仁
        var allSections = sections.Concat(indicatorSectionData).ToList();

        // 11. 公式求值体系
        // 11.1 PLItem 公式求值（FNodeRole=="formula" 且 FFormula 非空；含指标分区内的公式项）
        // 实体查找表用 allItems：指标分区项已从 plItems 分离，但其公式仍须求值
        EvaluateFormulaItems(allSections, allItems, periods.Count);

        // 11.2 Section(Tab root) 公式求值（root.FFormula 非空时）
        EvaluateSectionFormulas(sectionRootMap, periods, periods.Count);

        // 11.3 全局汇总行公式求值（itemAmounts 覆盖主分区与指标分区，全局公式可引用指标项）
        var globalSummaries = EvaluateGlobalSummaries(globalFormulaRoots, allSections, periods);

        // 12. 计算环比/同比变化率（含指标分区）
        foreach (var section in allSections)
        {
            foreach (var item in section.Items)
            {
                if (item.PeriodValues.Count >= 2)
                {
                    var cur = item.PeriodValues[0].Amount;
                    var mom = item.PeriodValues[1].Amount;
                    if (mom != 0)
                        item.MomChange = (cur - mom) / Math.Abs(mom) * 100m;
                }
                if (request.IncludeYoy && item.PeriodValues.Count >= 3)
                {
                    var cur = item.PeriodValues[0].Amount;
                    var yoy = item.PeriodValues[2].Amount;
                    if (yoy != 0)
                        item.YoyChange = (cur - yoy) / Math.Abs(yoy) * 100m;
                }
            }
        }

        // 13. 边际利润汇总（Summary.MarginTotals）从全局汇总行计算
        var marginTotals = new List<PeriodValue>();
        if (globalSummaries.Count > 0)
        {
            for (int p = 0; p < periods.Count; p++)
            {
                decimal sum = globalSummaries.Sum(gs =>
                    gs.PeriodValues.ElementAtOrDefault(p)?.Amount ?? 0m);
                marginTotals.Add(new PeriodValue { PeriodLabel = periods[p], Amount = sum });
            }
        }

        // 构建 TabNodes 响应
        var tabNodes = tabRoots.Select(t => new TabNodeDto
        {
            Id = t.FID,
            Name = t.FItemName,
            Sort = t.FSort,
            FormulaValue = sections.FirstOrDefault(s => s.TabAncestorId == t.FID)?.SectionTotals?.FirstOrDefault()?.Amount
        }).ToList();

        return new AmoebaMultiPeriodResponse
        {
            TabNodes = tabNodes,
            GlobalSummaries = globalSummaries,
            Sections = sections,
            PeriodLabels = periods,
            UnmatchedWarnings = unmatchedWarnings.Count > 0 ? unmatchedWarnings : null,
            IndicatorSections = indicatorSectionData.Count > 0 ? indicatorSectionData : null,
            Summary = new MultiPeriodSummary
            {
                MarginTotals = marginTotals,
                CurrentPeriodTickets = currentPeriodTickets,
            },
        };
    }

    // ===== 多期报表辅助方法 =====

    /// <summary>判断某节点是否是指定祖先的后裄节点</summary>
    private static bool IsDescendantOf(FinAmoebaPLItem item, long ancestorId, List<FinAmoebaPLItem> allItems)
    {
        var visited = new HashSet<long>();
        var current = item;
        while (current != null && current.FParentId != 0)
        {
            if (!visited.Add(current.FID)) return false; // 历史脏数据成环时终止，避免请求挂死
            if (current.FParentId == ancestorId) return true;
            current = allItems.FirstOrDefault(i => i.FID == current.FParentId);
        }
        return false;
    }

    // ===== [批次5-S3] 周期粒度泛化（day/week/month/quarter/year）。设计 spec §5.3。 =====

    /// <summary>归一化粒度：null/空→month；统一小写；非法值抛 ArgumentException。</summary>
    internal static string NormalizeGranularity(string? granularity)
    {
        var g = string.IsNullOrWhiteSpace(granularity) ? "month" : granularity.Trim().ToLowerInvariant();
        return g switch
        {
            "day" or "week" or "month" or "quarter" or "year" => g,
            _ => throw new ArgumentException($"不支持的周期粒度：{granularity}"),
        };
    }

    /// <summary>台账粒度(月/季/年走凭证+折旧实时聚合) vs 估算粒度(日/周走估算)。设计 §5.1 裁决C1。</summary>
    internal static bool IsLedgerGranularity(string? granularity) =>
        NormalizeGranularity(granularity) is "month" or "quarter" or "year";

    /// <summary>期间是否已结(完全过去)：末日 &lt; asOf 当日。已结台账期用凭证、未结(当月/未来)叠加估值。</summary>
    internal static bool IsPeriodClosed(string period, string granularity, DateTime asOf)
        => PeriodToDateRange(period, granularity).End < asOf.Date;

    /// <summary>
    /// 按粒度选数据源（设计 §5.1 裁决C1 + 当月例外）：
    /// billing 两类粒度共享(按日期可裁)；台账粒度(月/季/年)恒用 voucher+depreciation(本期发生额/折旧)，
    /// 仅当期间**未结**(periodClosed=false，当月/未来：账期未结、凭证不全)才叠加 estimate 作临时填充，
    /// 已结过往期间不取 estimate(改用凭证实时聚合)；估算粒度(日/周)恒用 estimate、无 voucher/depreciation。
    /// </summary>
    internal static (bool Billing, bool Voucher, bool Depreciation, bool Estimate) SelectSources(string? granularity, bool periodClosed)
    {
        bool ledger = IsLedgerGranularity(granularity);
        return (Billing: true, Voucher: ledger, Depreciation: ledger, Estimate: ledger ? !periodClosed : true);
    }

    /// <summary>期间所属月（起始日所在月，YYYYMM）。日/周公共费按"当月实际"摊取此月为基线。设计 §5.5/决策3。</summary>
    internal static string PeriodContainingMonth(string period, string granularity = "month")
        => PeriodToDateRange(period, granularity).Start.ToString("yyyyMM");

    /// <summary>期间键 = 粒度前缀 + 期间（设计 §5.3）：D:/W:/M:/Q:/Y:。存量(全月)迁移回填 'M:'+期间。</summary>
    internal static string BuildPeriodKey(string period, string granularity = "month")
    {
        var prefix = NormalizeGranularity(granularity) switch
        {
            "day" => "D",
            "week" => "W",
            "quarter" => "Q",
            "year" => "Y",
            _ => "M",
        };
        return $"{prefix}:{period}";
    }

    /// <summary>解析 ISO 周字符串 "YYYY-Www"（如 2026-W11）→ 该周周一；非法返回 false。</summary>
    private static bool TryParseIsoWeek(string? period, out DateTime monday)
    {
        monday = default;
        if (string.IsNullOrEmpty(period) || period.Length != 8 || period[4] != '-' || period[5] != 'W')
            return false;
        if (!int.TryParse(period.AsSpan(0, 4), out var year) || year is < 1900 or > 9999) return false;
        if (!int.TryParse(period.AsSpan(6, 2), out var week) || week < 1) return false;
        if (week > ISOWeek.GetWeeksInYear(year)) return false;
        monday = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
        return true;
    }

    /// <summary>DateTime → ISO 周字符串 "YYYY-Www"（ISO 年可能与日历年在年初/末跨界）。</summary>
    private static string FormatIsoWeek(DateTime date) =>
        $"{ISOWeek.GetYear(date):D4}-W{ISOWeek.GetWeekOfYear(date):D2}";

    /// <summary>校验期间字符串与粒度匹配（设计 §5.3 格式表）。granularity 默认 month 保持旧调用兼容。</summary>
    internal static bool IsValidPeriod(string? period, string granularity = "month")
    {
        if (string.IsNullOrWhiteSpace(period)) return false;
        switch (NormalizeGranularity(granularity))
        {
            case "day":
                return period.Length == 8
                    && DateTime.TryParseExact(period, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            case "week":
                return TryParseIsoWeek(period, out _);
            case "quarter":
                return period.Length == 6 && period[4] == 'Q'
                    && int.TryParse(period.AsSpan(0, 4), out var qy) && qy is >= 1900 and <= 9999
                    && int.TryParse(period.AsSpan(5, 1), out var qn) && qn is >= 1 and <= 4;
            case "year":
                return period.Length == 4
                    && int.TryParse(period, out var yy) && yy is >= 1900 and <= 9999;
            default: // month
                return period.Length == 6
                    && int.TryParse(period.AsSpan(0, 4), out var my) && my is >= 1900 and <= 9999
                    && int.TryParse(period.AsSpan(4, 2), out var mm) && mm is >= 1 and <= 12;
        }
    }

    /// <summary>
    /// 期间字符串 → (起始, 闭区间末日)。末日取「本期最后一天」，聚合内部 &lt; end.AddDays(1)
    /// 自动转半开 [start, nextStart)（与 AggregateVoucherData 的半开过滤一致）。设计 §5.3。
    /// </summary>
    internal static (DateTime Start, DateTime End) PeriodToDateRange(string period, string granularity = "month")
    {
        var g = NormalizeGranularity(granularity);
        if (!IsValidPeriod(period, g))
            throw new ArgumentException($"期间格式与粒度({g})不符，当前值：{period}");
        switch (g)
        {
            case "day":
            {
                var d = DateTime.ParseExact(period, "yyyyMMdd", CultureInfo.InvariantCulture);
                return (d, d);
            }
            case "week":
            {
                TryParseIsoWeek(period, out var monday);
                return (monday, monday.AddDays(6));
            }
            case "quarter":
            {
                var qy = int.Parse(period.AsSpan(0, 4));
                var qn = int.Parse(period.AsSpan(5, 1));
                var start = new DateTime(qy, (qn - 1) * 3 + 1, 1);
                return (start, start.AddMonths(3).AddDays(-1));
            }
            case "year":
            {
                var y = int.Parse(period);
                return (new DateTime(y, 1, 1), new DateTime(y, 12, 31));
            }
            default: // month
            {
                var year = int.Parse(period.AsSpan(0, 4));
                var month = int.Parse(period.AsSpan(4, 2));
                var start = new DateTime(year, month, 1);
                return (start, start.AddMonths(1).AddDays(-1));
            }
        }
    }

    /// <summary>上一期（按粒度）：月→上月, 日→昨日, 周→上周, 季→上季, 年→去年。</summary>
    internal static string CalcPreviousPeriod(string period, string granularity = "month")
    {
        switch (NormalizeGranularity(granularity))
        {
            case "day":
                return DateTime.ParseExact(period, "yyyyMMdd", CultureInfo.InvariantCulture).AddDays(-1).ToString("yyyyMMdd");
            case "week":
            {
                TryParseIsoWeek(period, out var monday);
                return FormatIsoWeek(monday.AddDays(-7));
            }
            case "quarter":
            {
                var qy = int.Parse(period.AsSpan(0, 4));
                var qn = int.Parse(period.AsSpan(5, 1));
                qn--; if (qn == 0) { qn = 4; qy--; }
                return $"{qy:D4}Q{qn}";
            }
            case "year":
                return $"{int.Parse(period) - 1:D4}";
            default: // month
            {
                var year = int.Parse(period.AsSpan(0, 4));
                var month = int.Parse(period.AsSpan(4, 2));
                return new DateTime(year, month, 1).AddMonths(-1).ToString("yyyyMM");
            }
        }
    }

    /// <summary>去年同期：月/季/年→年-1；日/周→null（无同比意义，设计 §5.3）。</summary>
    internal static string? CalcYoyPeriod(string period, string granularity = "month")
    {
        switch (NormalizeGranularity(granularity))
        {
            case "month":
            {
                var year = int.Parse(period.AsSpan(0, 4));
                var month = int.Parse(period.AsSpan(4, 2));
                return $"{year - 1:D4}{month:D2}";
            }
            case "quarter":
            {
                var qy = int.Parse(period.AsSpan(0, 4));
                var qn = int.Parse(period.AsSpan(5, 1));
                return $"{qy - 1:D4}Q{qn}";
            }
            case "year":
                return $"{int.Parse(period) - 1:D4}";
            default: // day, week
                return null;
        }
    }

    /// <summary>
    /// 对带 FFormula 的项目（formula/indicator，以及非顶级 group——其公式结果覆盖 SUM_CHILDREN），
    /// 使用公式引擎求值并回填 Amount。支持 ${项目名称} 引用其他项的已计算金额。
    /// </summary>
    private void EvaluateFormulaItems(List<SectionData> sections, List<FinAmoebaPLItem> plItems, int periodCount)
    {
        // 构建所有公式项的 entity 查找表
        var formulaItemEntities = plItems
            .Where(i => (i.FNodeRole == "formula" || i.FNodeRole == "indicator" || i.FNodeRole == "group")
                        && !string.IsNullOrWhiteSpace(i.FFormula))
            .ToDictionary(i => i.FID, i => i);

        if (formulaItemEntities.Count == 0) return;

        // 对每个期间独立求值
        for (int p = 0; p < periodCount; p++)
        {
            // 构建“项目名称 → 金额”查找表（包含所有非公式项的已计算值 + 分区小计）
            var itemAmounts = new Dictionary<string, decimal>();
            foreach (var section in sections)
            {
                // 分区小计也可以被引用
                if (section.SectionTotals != null && p < section.SectionTotals.Count)
                {
                    itemAmounts[section.SectionName] = section.SectionTotals[p].Amount;
                }
                foreach (var item in section.Items)
                {
                    if (p < item.PeriodValues.Count)
                    {
                        itemAmounts[item.Name] = item.PeriodValues[p].Amount;
                    }
                }
            }

            // 公式可链式引用公式：迭代求值直到收敛（无值变化）或达到轮次上限
            var maxPasses = Math.Min(formulaItemEntities.Count + 1, 8);
            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool changed = false;
                foreach (var section in sections)
                {
                    foreach (var item in section.Items)
                    {
                        if (!formulaItemEntities.TryGetValue(item.Id, out var entity)) continue;

                        var context = new FormulaContext { ItemAmounts = itemAmounts };
                        try
                        {
                            var result = _formulaEngine.Evaluate(entity.FFormula!, context);
                            if (p < item.PeriodValues.Count && item.PeriodValues[p].Amount != result)
                            {
                                item.PeriodValues[p].Amount = result;
                                changed = true;
                            }
                            // 更新查找表以便后续公式可引用此项
                            itemAmounts[item.Name] = result;
                        }
                        catch
                        {
                            // 公式求值失败时保持原值，不影响其他项
                        }
                    }
                }
                if (!changed) break;
            }
        }
    }

    /// <summary>
    /// Section(Tab root) 公式求值：对有 FFormula 的 depth=0 group 节点，求值后填充 SectionTotals
    /// </summary>
    private void EvaluateSectionFormulas(
        List<(SectionData Section, FinAmoebaPLItem Root)> sectionRootMap,
        List<string> periods,
        int periodCount)
    {
        foreach (var (section, root) in sectionRootMap)
        {
            if (string.IsNullOrWhiteSpace(root.FFormula)) continue;

            section.SectionTotals = new List<PeriodValue>();
            for (int p = 0; p < periodCount; p++)
            {
                // 构建当期名称 → 金额查找表
                var itemAmounts = new Dictionary<string, decimal>();
                foreach (var item in section.Items)
                {
                    if (p < item.PeriodValues.Count)
                        itemAmounts[item.Name] = item.PeriodValues[p].Amount;
                }

                var ctx = new FormulaContext { ItemAmounts = itemAmounts };
                decimal amount = 0m;
                try
                {
                    amount = _formulaEngine.Evaluate(root.FFormula!, ctx);
                }
                catch { }

                section.SectionTotals.Add(new PeriodValue
                {
                    PeriodLabel = p < periods.Count ? periods[p] : "",
                    Amount = amount
                });
            }
        }
    }

    /// <summary>
    /// 构建 TabAncestorId 映射：为每个节点计算其 depth=0 的 group 祖先 ID
    /// </summary>
    private static Dictionary<long, long> BuildTabAncestorMap(List<FinAmoebaPLItem> plItems, List<FinAmoebaPLItem> tabRoots)
    {
        var map = new Dictionary<long, long>();
        var itemDict = plItems.ToDictionary(i => i.FID);
        var tabRootIds = new HashSet<long>(tabRoots.Select(t => t.FID));

        foreach (var item in plItems)
        {
            if (tabRootIds.Contains(item.FID))
            {
                map[item.FID] = item.FID;
                continue;
            }

            // 向上追溯直到 FParentId==0 的节点
            var current = item;
            var visited = new HashSet<long> { current.FID };
            while (current.FParentId != 0 && itemDict.ContainsKey(current.FParentId))
            {
                current = itemDict[current.FParentId];
                if (!visited.Add(current.FID)) break; // 历史脏数据成环时终止，避免请求挂死
            }
            if (tabRootIds.Contains(current.FID))
            {
                map[item.FID] = current.FID;
            }
        }

        return map;
    }

    /// <summary>
    /// SUM_CHILDREN 汇总逻辑（depth>0 group）：
    /// 只累加直接子项中 FNodeRole=="data" 和 FNodeRole=="group" 的值，
    /// 显式排除 formula 和 indicator。
    /// 如果 group 自身有 FFormula，则公式结果覆盖 SUM_CHILDREN。
    /// </summary>
    private decimal SumChildrenForGroup(FinAmoebaPLItem groupItem, List<FinAmoebaPLItem> all,
        Dictionary<long, decimal> matchedAmounts,
        Dictionary<(string, long?), FinAmoebaManualData> manualLookup,
        string periodKey)   // [批次5-S3] manualLookup 按期间键
    {
        var directChildren = all.Where(i => i.FParentId == groupItem.FID).ToList();
        if (directChildren.Count == 0) return 0m;

        // 只累加 data 和 group 子项
        var eligibleChildren = directChildren.Where(c => c.FNodeRole == "data" || c.FNodeRole == "group").ToList();
        if (eligibleChildren.Count == 0) return 0m;

        decimal sum = 0m;
        foreach (var child in eligibleChildren)
        {
            if (child.FNodeRole == "group")
            {
                // 递归求子 group 的 SUM_CHILDREN
                sum += SumChildrenForGroup(child, all, matchedAmounts, manualLookup, periodKey);
            }
            else if (child.FIsManualEntry)
            {
                if (manualLookup.TryGetValue((periodKey, (long?)child.FID), out var md))
                    sum += md.FAmount;
            }
            else
            {
                if (matchedAmounts.TryGetValue(child.FID, out var amt))
                    sum += amt;
            }
        }
        return sum;
    }

    /// <summary>
    /// 全局汇总行公式求值：对 FParentId==0 且 FNodeRole=="formula" 的节点求值
    /// </summary>
    private List<GlobalSummaryDto> EvaluateGlobalSummaries(
        List<FinAmoebaPLItem> globalFormulaRoots,
        List<SectionData> sections,
        List<string> periods)
    {
        var result = new List<GlobalSummaryDto>();
        if (globalFormulaRoots.Count == 0) return result;

        foreach (var root in globalFormulaRoots)
        {
            var dto = new GlobalSummaryDto
            {
                Id = root.FID,
                Name = root.FItemName,
                Formula = root.FFormula,
                Unit = root.FUnit,
            };

            for (int p = 0; p < periods.Count; p++)
            {
                decimal? amount = null;
                if (!string.IsNullOrWhiteSpace(root.FFormula))
                {
                    var itemAmounts = BuildFormulaItemAmounts(sections, p);
                    foreach (var previous in result)
                    {
                        if (previous.PeriodValues.Count > p)
                        {
                            itemAmounts[previous.Name] = previous.PeriodValues[p].Amount;
                        }
                    }

                    var ctx = new FormulaContext { ItemAmounts = itemAmounts };
                    try
                    {
                        amount = _formulaEngine.Evaluate(root.FFormula!, ctx);
                    }
                    catch
                    {
                        amount = null;
                    }
                }

                dto.PeriodValues.Add(new PeriodValue
                {
                    PeriodLabel = periods[p],
                    Amount = amount ?? 0m
                });
            }

            dto.Value = dto.PeriodValues.FirstOrDefault()?.Amount;
            result.Add(dto);
        }

        return result;
    }

    private static Dictionary<string, decimal> BuildFormulaItemAmounts(List<SectionData> sections, int periodIndex)
    {
        var itemAmounts = new Dictionary<string, decimal>();
        foreach (var section in sections)
        {
            if (section.SectionTotals != null && section.SectionTotals.Count > periodIndex)
                itemAmounts[section.SectionName] = section.SectionTotals[periodIndex].Amount;
            foreach (var item in section.Items)
            {
                if (item.PeriodValues.Count > periodIndex)
                    itemAmounts[item.Name] = item.PeriodValues[periodIndex].Amount;
            }
        }
        return itemAmounts;
    }

    /// <summary>
    /// 独占匹配：按 FSort 顺序遍历叶子损益项，将 DataPoint 一次性归属到首个匹配的项
    /// 按数据源分支：
    ///   - billing: 通过 FBillingFilterJson (outlets/businessObjects) 过滤
    ///   - voucher/depreciation/estimate: 科目编码前缀匹配 + 科目级独立辅助过滤
    /// 返回：(plItemId -> 累计金额, 未匹配数据点列表)
    /// </summary>
    internal (Dictionary<long, decimal> Matched, List<DataPoint> Unmatched) MatchDataPointsToPLItems(
        List<DataPoint> dataPoints, List<FinAmoebaPLItem> plItems)
    {
        var matched = new Dictionary<long, decimal>();
        // 对 FNodeRole=="data" 或 "indicator" 且非手工填报的项参与匹配。
        // 独占匹配顺序按模板树前序（文档顺序）：FSort 是兄弟内序号，跨分区直接全局排序会让
        // "谁先抢到重叠科目的数据"取决于不相关分区间的序号交错，结果不可预期
        var preorderIndex = BuildPreorderIndex(plItems);
        var leafItems = plItems
            .Where(i => (i.FNodeRole == "data" || i.FNodeRole == "indicator") && !i.FIsManualEntry)
            .OrderBy(i => preorderIndex.GetValueOrDefault(i.FID, int.MaxValue))
            .ToList();

        // 候选池（独占匹配后从池中移除）
        var pool = new List<DataPoint>(dataPoints);

        foreach (var leaf in leafItems)
        {
            var acceptableSources = GetAcceptableSourcesForLeaf(leaf);
            if (acceptableSources.Count == 0) continue;

            decimal sum = 0m;
            var taken = new List<DataPoint>();

            if (leaf.FDataSource == "billing" || leaf.F系统数据源 == "billing")
            {
                // 计费分支：通过 FBillingFilterJson 过滤
                var billingFilter = ParseBillingFilter(leaf.FBillingFilterJson);

                foreach (var dp in pool)
                {
                    if (!acceptableSources.Contains(dp.Source)) continue;

                    // scope 过滤：priced 模式只取已计价数据
                    if (billingFilter == null || billingFilter.Scope == "priced")
                    {
                        if (!dp.IsPriced) continue;
                    }
                    // scope == "all" 时不检查 IsPriced

                    // BillingFilter为null = 不过滤，全量匹配
                    if (billingFilter != null)
                    {
                        // outlets非空且dp.SiteCode不在其中 → skip
                        if (billingFilter.Outlets.Count > 0 &&
                            (string.IsNullOrEmpty(dp.SiteCode) || !billingFilter.Outlets.Contains(dp.SiteCode)))
                            continue;

                        // businessObjects非空且dp业务对象不在其中 → skip
                        if (billingFilter.BusinessObjects.Count > 0)
                        {
                            var boCode = dp.AuxValues?.GetValueOrDefault("business_object");
                            if (string.IsNullOrEmpty(boCode) || !billingFilter.BusinessObjects.Contains(boCode))
                                continue;
                        }
                    }

                    // 根据 aggregation 决定取值
                    var value = billingFilter?.Aggregation switch
                    {
                        "waybill_count" => dp.WaybillCount,
                        "weight" => dp.Weight,
                        _ => dp.Amount  // "amount" 或默认
                    };
                    sum += value;
                    taken.Add(dp);
                }
            }
            else
            {
                // 凭证/折旧/暂估分支 - 科目级独立辅助过滤
                var accountsWithFilter = ParseAccountCodesV2(leaf.FRelatedAccountsJson)
                    .Where(a => !string.IsNullOrEmpty(a.Code))
                    .ToList();

                if (AccountLeafIsExcluded(leaf, accountsWithFilter)) continue;

                foreach (var dp in pool)
                {
                    if (!AccountLeafAccepts(dp, acceptableSources, accountsWithFilter)) continue;

                    sum += dp.Amount;
                    taken.Add(dp);
                }
            }

            if (taken.Count > 0)
            {
                matched[leaf.FID] = sum;
                // 计费分支不移除：同一组 billing DP 可被不同聚合方式(金额/件量/重量)的项目重复使用
                // 凭证/折旧/暂估分支保持独占（同一笔凭证金额不应重复计入）
                if (!(leaf.FDataSource == "billing" || leaf.F系统数据源 == "billing"))
                {
                    foreach (var t in taken) pool.Remove(t);
                }
            }
        }

        return (matched, pool);
    }

    /// <summary>
    /// 构建模板树前序遍历的顺序索引（文档顺序）：根按 FSort，子级递归按 FSort。
    /// 传入子集（如指标分区项）时，父级不在集合内的节点视为根。带防环守卫。
    /// </summary>
    private static Dictionary<long, int> BuildPreorderIndex(List<FinAmoebaPLItem> items)
    {
        var index = new Dictionary<long, int>();
        var ids = items.Select(i => i.FID).ToHashSet();
        var childrenLookup = items.ToLookup(i => i.FParentId);
        var counter = 0;

        void Visit(FinAmoebaPLItem node)
        {
            if (index.ContainsKey(node.FID)) return; // 防环
            index[node.FID] = counter++;
            foreach (var child in childrenLookup[node.FID].OrderBy(c => c.FSort).ThenBy(c => c.FID))
                Visit(child);
        }

        var roots = items
            .Where(i => i.FParentId == 0 || !ids.Contains(i.FParentId))
            .OrderBy(i => i.FSort)
            .ThenBy(i => i.FID);
        foreach (var root in roots) Visit(root);
        return index;
    }

    /// <summary>
    /// 凭证/折旧/暂估类叶子项是否整体不参与匹配。
    /// voucher 源未配置关联科目的项不参与：若放行，科目过滤整体失效，
    /// 该项会独占吞掉池中全部凭证/暂估数据点，导致排序靠后的项全部归零。
    /// depreciation/estimate 源保留"无科目=全取本源"的旧约定（与折旧下钻
    /// GetDepreciationDrillDownAsync 的语义一致，且两类数据池与凭证池互不竞争）。
    /// </summary>
    private static bool AccountLeafIsExcluded(FinAmoebaPLItem leaf, List<AccountWithFilter> accountsWithFilter)
    {
        var effectiveSource = leaf.F系统数据源 ?? leaf.FDataSource;
        return accountsWithFilter.Count == 0 && effectiveSource == "voucher";
    }

    /// <summary>
    /// 凭证/折旧/暂估分支的单点匹配判定：数据源类型隔离 + 科目前缀 + 科目级辅助过滤。
    /// 独占匹配管道与损益项明细下钻共用此判定，防止两处口径漂移。
    /// </summary>
    private static bool AccountLeafAccepts(DataPoint dp, HashSet<string> acceptableSources, List<AccountWithFilter> accountsWithFilter)
    {
        // 第一层：数据源类型隔离
        if (!acceptableSources.Contains(dp.Source)) return false;

        // 第二层：科目匹配 + 科目级辅助过滤（无科目配置时全取，仅 depreciation/estimate 源可走到这里）
        if (accountsWithFilter.Count > 0)
        {
            if (string.IsNullOrEmpty(dp.AccountCode)) return false;

            return accountsWithFilter.Any(a =>
                dp.AccountCode.StartsWith(a.Code)
                && AuxiliaryMatches(dp, a.Filters));
        }
        return true;
    }

    /// <summary>
    /// 解析新格式 F关联科目JSON：[{"code":"54010101","filters":[{"auxType":"outlet","codes":["SC001"]}]}, ...]
    /// 兼容旧格式 ["54010101","540102"]。
    /// </summary>
    private static List<AccountWithFilter> ParseAccountCodesV2(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<AccountWithFilter>();
        try
        {
            var items = JsonSerializer.Deserialize<List<AccountWithFilter>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return items ?? new List<AccountWithFilter>();
        }
        catch
        {
            try
            {
                var codes = JsonSerializer.Deserialize<List<string>>(json);
                return (codes ?? new List<string>())
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Select(code => new AccountWithFilter { Code = code })
                    .ToList();
            }
            catch
            {
                return new List<AccountWithFilter>();
            }
        }
    }

    private static List<string> ParseAccountCodePrefixes(string? json)
    {
        return ParseAccountCodesV2(json)
            .Select(a => a.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToList();
    }

    /// <summary>解析计费数据源过滤条件JSON</summary>
    private static BillingFilter? ParseBillingFilter(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var filter = new BillingFilter();
            if (root.TryGetProperty("outlets", out var outlets) && outlets.ValueKind == JsonValueKind.Array)
            {
                foreach (var o in outlets.EnumerateArray())
                    if (o.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(o.GetString()))
                        filter.Outlets.Add(o.GetString()!);
            }
            if (root.TryGetProperty("businessObjects", out var bos) && bos.ValueKind == JsonValueKind.Array)
            {
                foreach (var b in bos.EnumerateArray())
                    if (b.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(b.GetString()))
                        filter.BusinessObjects.Add(b.GetString()!);
            }
            if (root.TryGetProperty("aggregation", out var agg) && agg.ValueKind == JsonValueKind.String)
                filter.Aggregation = agg.GetString() ?? "amount";
            if (root.TryGetProperty("scope", out var sc) && sc.ValueKind == JsonValueKind.String)
                filter.Scope = sc.GetString() ?? "priced";
            return filter;
        }
        catch { return null; }
    }

    /// <summary>科目编码前缀匹配（任一前缀命中即视为匹配）</summary>
    private static bool CodesMatch(string accountCode, List<string> codes)
    {
        return codes.Any(c => !string.IsNullOrEmpty(c) && accountCode.StartsWith(c));
    }

    /// <summary>
    /// 数据源兼容映射：决定某 FDataSource 类型的损益项可接受哪些来源的 DataPoint。
    /// voucher 同时接受 estimate（暂估），其余仅自身。
    /// </summary>
    private static HashSet<string> GetAcceptableSources(string? dataSource) => dataSource switch
    {
        "voucher" => new HashSet<string> { "voucher", "estimate" },
        "estimate" => new HashSet<string> { "estimate" },
        "billing" => new HashSet<string> { "billing" },
        "depreciation" => new HashSet<string> { "depreciation" },
        _ => new HashSet<string>()
    };

    /// <summary>
    /// 按新字段 F系统数据源 获取可接受来源，带 fallback 到旧字段 FDataSource。
    /// </summary>
    private static HashSet<string> GetAcceptableSourcesForLeaf(FinAmoebaPLItem leaf)
    {
        // 新字段优先，若为空则 fallback 到旧字段
        var effectiveSource = leaf.F系统数据源 ?? leaf.FDataSource;
        return GetAcceptableSources(effectiveSource);
    }

    /// <summary>
    /// 辅助核算匹配（AND 语义）：所有过滤条件都要命中才视为匹配。
    /// 取值优先使用快捷字段（SiteCode/BrandCode/BusinessUnitId），否则回落到 AuxValues 字典。
    /// </summary>
    private static bool AuxiliaryMatches(DataPoint dp, List<AuxFilter>? filters)
    {
        if (filters == null || filters.Count == 0) return true;

        foreach (var f in filters)
        {
            string? dpValue = f.AuxType switch
            {
                "outlet" => dp.SiteCode ?? dp.AuxValues?.GetValueOrDefault("outlet"),
                "express_brand" => dp.BrandCode ?? dp.AuxValues?.GetValueOrDefault("express_brand"),
                "business_unit" => dp.BusinessUnitId?.ToString() ?? dp.AuxValues?.GetValueOrDefault("business_unit"),
                "business_object" => dp.AuxValues?.GetValueOrDefault("business_object"),
                // 方案B 源打标(B1)：方向/项目/部门显式化。business_direction 优先读 AuxValues(OUT/IN/CMB)，
                // 回落 dp.Direction(批次3 后 Direction 统一为 OUT/IN/CMB，此 fallback 退役)。
                "business_direction" => dp.AuxValues?.GetValueOrDefault("business_direction") ?? dp.Direction,
                "project" => dp.AuxValues?.GetValueOrDefault("project"),
                "department" => dp.AuxValues?.GetValueOrDefault("department"),
                _ => dp.AuxValues?.GetValueOrDefault(f.AuxType)
            };

            if (string.IsNullOrEmpty(dpValue) || !f.Codes.Contains(dpValue))
                return false;
        }
        return true;
    }

    /// <summary>
    /// [方案B 批次4] L1 请求级作用域预过滤：在独占匹配(MatchDataPointsToPLItems)之前对 DataPoint 池过滤。
    /// 维度内 OR、跨维度 AND(复用 AuxiliaryMatches);某维度被约束时该维度为空的点被剔除(严格语义,
    /// 子报表正确性必须——否则掺入无网点归属的公共费)。scope=null 或全空 → 原样返回(全口径)。
    /// 不 clone DataPoint(与独占匹配的 consumed 引用相等共存)。
    /// </summary>
    internal static List<DataPoint> ApplyScopeFilter(List<DataPoint> points, AmoebaReportScope? scope)
    {
        if (scope == null) return points;
        var filters = new List<AuxFilter>();
        if (scope.Outlets is { Count: > 0 }) filters.Add(new AuxFilter { AuxType = "outlet", Codes = scope.Outlets });
        if (scope.Projects is { Count: > 0 }) filters.Add(new AuxFilter { AuxType = "project", Codes = scope.Projects });
        if (scope.Directions is { Count: > 0 }) filters.Add(new AuxFilter { AuxType = "business_direction", Codes = scope.Directions });
        if (scope.Units is { Count: > 0 }) filters.Add(new AuxFilter { AuxType = "business_unit", Codes = scope.Units.Select(x => x.ToString()).ToList() });
        if (scope.Brands is { Count: > 0 }) filters.Add(new AuxFilter { AuxType = "express_brand", Codes = scope.Brands });
        if (filters.Count == 0) return points;
        return points.Where(p => AuxiliaryMatches(p, filters)).ToList();
    }

    // ===== [批次5-S2] 公共费按件量分摊「接入」辅助（纯函数，引擎在 CommonCostAllocationEngine）=====

    /// <summary>公共费叶判定：F分摊方式=="volume"（大小写不敏感）。设计 spec §4.1。</summary>
    internal static bool IsCommonCostLeaf(FinAmoebaPLItem leaf)
        => string.Equals(leaf.F分摊方式, "volume", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 解析发件/派件票量 indicator 的 FID（FNodeRole=="indicator" 且名称含"发件票量"/"派件票量"）。
    /// 供公共费分摊 ComputeVolumeBasis(§4.2) 与单票方向化 ComputePerUnit(§5.4, 批次5-S5) 共用。
    /// </summary>
    internal static (long? SendId, long? DeliverId) ResolveVolumeIndicatorIds(List<FinAmoebaPLItem> items)
    {
        long? sendId = null, deliverId = null;
        foreach (var i in items)
        {
            if (i.FNodeRole != "indicator") continue;
            var name = i.FItemName ?? "";
            if (sendId == null && name.Contains("发件票量")) sendId = i.FID;
            if (deliverId == null && name.Contains("派件票量")) deliverId = i.FID;
        }
        return (sendId, deliverId);
    }

    /// <summary>
    /// 计算件量基数（设计 spec §4.2 取值优先级 / 派件量接入 §B1）：
    /// send = 出港计费 SUM(WaybillCount)（billing 源天然出港）→ 回退发件票量 indicator 匹配值；
    /// deliver = 调用方传入（来自 STG申通派件日明细 SUM([F基础派费收费件量])，见 AggregateDeliverVolume）。
    /// 旧逻辑从 matched 取派件票量 indicator 恒为 0（派件票量是手工节点不进 matched），故改为外部注入。
    /// </summary>
    internal static VolumeBasis ComputeVolumeBasis(
        List<DataPoint> points, IReadOnlyDictionary<long, decimal> matched, long? sendId, long deliverCount)
    {
        long billingSend = points.Where(p => p.Source == "billing").Sum(p => (long)p.WaybillCount);
        long send = billingSend > 0
            ? billingSend
            : (sendId is long sid && matched.TryGetValue(sid, out var sv) ? (long)sv : 0L);
        return new VolumeBasis { SendCount = send, DeliverCount = deliverCount };
    }

    // ===== [批次5-S5] 单票&均方向化（设计 spec §5.4）=====

    /// <summary>Tab 名→方向：含"出港"→OUT、含"进港"→IN、其余(公共/综合/指标)→CMB。</summary>
    internal static string TabToDirection(string? tabName)
    {
        var n = tabName ?? "";
        if (n.Contains("出港")) return "OUT";
        if (n.Contains("进港")) return "IN";
        return "CMB";
    }

    /// <summary>
    /// 单票均值：按方向选件量分母——出港÷发件票量、进港÷派件票量、公共÷合计(发件+派件)；
    /// 分母≤0 返 null(不显示单票)。设计 §5.4。
    /// </summary>
    internal static decimal? PerUnitValue(string? direction, decimal amount, decimal sendCount, decimal deliverCount)
    {
        decimal denom = direction switch
        {
            "OUT" => sendCount,
            "IN" => deliverCount,
            _ => sendCount + deliverCount,
        };
        return denom > 0 ? amount / denom : (decimal?)null;
    }

    /// <summary>
    /// 子报表公共费按件量分摊注入（设计 spec §4.4/§4.5）：以 scope 过滤前的全口径基线(fullPoints)
    /// 算各公共费叶全额 + 全口径件量，按 scope件量/全口径件量 比例分摊，覆盖 scopedMatched[叶FID]。
    /// 仅在子报表(IsSubReport)时调用；全口径(scope=null)公共费叶走正常科目匹配，不进此方法。
    /// fullPoints 为同期 scope 过滤前的全集——多期路径聚合本身无 SQL 级 scope(过滤在内存)，故零额外 DB 聚合即得基线。
    /// 子报表口径下公共费叶值=分摊额或缺失，绝不保留直接归段残值(send 类公共费会随 scope 存活，须显式移除)。
    /// </summary>
    internal void ApplyCommonCostAllocation(
        Dictionary<long, decimal> scopedMatched,
        List<DataPoint> scopedPoints,
        List<DataPoint> fullPoints,
        List<FinAmoebaPLItem> plItems,
        List<FinAmoebaPLItem> indicatorItems,
        List<FinAmoebaPLItem> allItems,
        List<string> warnings,
        long scopedDeliver,
        long fullDeliver)
    {
        var commonLeaves = plItems.Where(IsCommonCostLeaf).ToList();
        if (commonLeaves.Count == 0) return;

        // 全口径基线匹配：取各公共费叶全额 + 票量 indicator 匹配值(含指标分区，镜像 scoped 路径的并入)
        var (fullMatched, _) = MatchDataPointsToPLItems(fullPoints, plItems);
        if (indicatorItems.Count > 0)
        {
            var (fullInd, _) = MatchDataPointsToPLItems(fullPoints, indicatorItems);
            foreach (var kv in fullInd) fullMatched[kv.Key] = kv.Value;
        }

        var fullTotals = new Dictionary<long, decimal>();
        foreach (var leaf in commonLeaves)
            if (fullMatched.TryGetValue(leaf.FID, out var t)) fullTotals[leaf.FID] = t;

        // deliver 件量改由调用方注入（scopedDeliver/fullDeliver 来自 AggregateDeliverVolume / STG申通派件日明细）；
        // deliverId 不再用于取数（旧逻辑从 matched 取派件票量 indicator 恒为 0），ResolveVolumeIndicatorIds 仅取 sendId。
        var (sendId, _) = ResolveVolumeIndicatorIds(allItems);
        var scopeVol = ComputeVolumeBasis(scopedPoints, scopedMatched, sendId, scopedDeliver);
        var fullVol = ComputeVolumeBasis(fullPoints, fullMatched, sendId, fullDeliver);

        var alloc = _allocEngine.Allocate(commonLeaves, fullTotals, scopeVol, fullVol, out var w);
        warnings.AddRange(w);

        // 注入：命中分摊→写分摊额；未命中(全额为0/缺失)→移除直接归段残值，保证公共费叶不出非分摊值
        foreach (var leaf in commonLeaves)
        {
            if (alloc.TryGetValue(leaf.FID, out var a)) scopedMatched[leaf.FID] = a;
            else scopedMatched.Remove(leaf.FID);
        }
    }

    /// <summary>损益项级辅助核算过滤条件</summary>
    private class AuxFilter
    {
        public string AuxType { get; set; } = string.Empty;
        public List<string> Codes { get; set; } = new();
    }

    /// <summary>科目+独立辅助过滤配置（新格式解析结果）</summary>
    private class AccountWithFilter
    {
        public string Code { get; set; } = string.Empty;
        public List<AuxFilter>? Filters { get; set; }
    }

    /// <summary>计费数据源过滤条件</summary>
    private class BillingFilter
    {
        public List<string> Outlets { get; set; } = new();
        public List<string> BusinessObjects { get; set; } = new();
        public string Aggregation { get; set; } = "amount";  // amount | waybill_count | weight
        public string Scope { get; set; } = "priced";        // priced | all
    }

    /// <summary>前序遍历收集子项（不含 root 自身），返回 (item, depth)。section 子项也参与。</summary>
    private void CollectChildrenPreorder(FinAmoebaPLItem root, List<FinAmoebaPLItem> all,
        List<(FinAmoebaPLItem, int)> output, int rootDepth)
    {
        var directChildren = all.Where(i => i.FParentId == root.FID).OrderBy(i => i.FSort).ToList();
        foreach (var child in directChildren)
        {
            output.Add((child, rootDepth + 1));
            CollectChildrenPreorder(child, all, output, rootDepth + 1);
        }
    }

    /// <summary>递归求 root 下所有叶子项的 Amount 之和（叶子=无子节点），排除 formula 和 indicator。
    /// manualLookup 按期间键 [批次5-S3]（与 SumChildrenForGroup 同口径）。</summary>
    private decimal SumLeafDescendants(FinAmoebaPLItem root, List<FinAmoebaPLItem> all,
        Dictionary<long, decimal> matchedAmounts,
        Dictionary<(string, long?), FinAmoebaManualData> manualLookup,
        string periodKey)
    {
        var directChildren = all.Where(i => i.FParentId == root.FID).ToList();
        if (directChildren.Count == 0)
        {
            // root 本身是叶子
            if (root.FNodeRole == "formula" || root.FNodeRole == "indicator") return 0m;
            if (root.FIsManualEntry)
            {
                return manualLookup.TryGetValue((periodKey, (long?)root.FID), out var md) ? md.FAmount : 0m;
            }
            return matchedAmounts.TryGetValue(root.FID, out var amt) ? amt : 0m;
        }
        decimal sum = 0m;
        foreach (var c in directChildren)
        {
            // 排除 formula 和 indicator 子项
            if (c.FNodeRole == "formula" || c.FNodeRole == "indicator") continue;
            sum += SumLeafDescendants(c, all, matchedAmounts, manualLookup, periodKey);
        }
        return sum;
    }

    /// <summary>
    /// 导出阿米巴经营报表为Excel
    /// </summary>
    public async Task<byte[]> ExportReportToExcelAsync(AmoebaReportRequest request, long accountSetId)
    {
        if (accountSetId > 0)
            request.AccountSetId = accountSetId;

        var orgId = request.OrgId > 0 ? request.OrgId : GetCurrentOrgId();
        var effectiveAccountSetId = request.AccountSetId;

        // 获取组织名称
        var orgName = await _dbContext.Set<SysOrganization>()
            .Where(o => o.FID == orgId)
            .Select(o => o.FName)
            .FirstOrDefaultAsync() ?? "未知组织";

        // 1. 加载映射规则
        var mappingRules = await _dbContext.Set<FinAmoebaMappingRule>()
            .Where(r => r.FOrgId == orgId)
            .OrderByDescending(r => r.FPriority)
            .ToListAsync();

        // 2. 加载损益模板和损益项
        long templateId = request.TemplateId;
        if (templateId == 0 && effectiveAccountSetId > 0)
        {
            var tpl = await _dbContext.Set<FinAmoebaPLTemplate>()
                .FirstOrDefaultAsync(t => t.FAccountSetId == effectiveAccountSetId);
            templateId = tpl?.FID ?? 0;
        }
        // 模板无法解析时使用空列表：决不能回落到全库损益项（跨模板/账套/组织串数据）
        var plItems = templateId > 0
            ? await _dbContext.Set<FinAmoebaPLItem>().Where(i => i.FTemplateId == templateId).ToListAsync()
            : new List<FinAmoebaPLItem>();

        // 3. 加载手工分类
        var manualClassifications = await _dbContext.Set<FinAmoebaManualClassify>()
            .Where(m => m.FOrgId == orgId)
            .ToListAsync();

        // 5. 加载经营单元
        var units = await _dbContext.Set<FinAuxiliaryItem>()
            .Where(u => u.FOrgId == orgId && u.FAuxType == "business_unit" && u.FEnableStatus == 1)
            .ToListAsync();

        // 6. 聚合数据
        var billingData = await AggregateBillingRevenue(request.StartDate, request.EndDate, orgId, plItems, mappingRules, request.UnitIds, request.SiteCodes);
        var voucherData = await AggregateVoucherData(request.StartDate, request.EndDate, orgId, effectiveAccountSetId);
        var depreciationData = await AggregateDepreciation(request.StartDate, request.EndDate, orgId, effectiveAccountSetId);

        var allDataPoints = new List<DataPoint>();
        allDataPoints.AddRange(billingData);
        allDataPoints.AddRange(voucherData);
        allDataPoints.AddRange(depreciationData);

        // 7. 方向分类
        foreach (var point in allDataPoints)
        {
            if (string.IsNullOrEmpty(point.Direction))
                point.Direction = ClassifyDirection(point, plItems, manualClassifications);
        }

        // 8. 映射到经营单元
        foreach (var point in allDataPoints)
        {
            point.MappedUnitId = MapToUnit(point, mappingRules);
        }

        // [批次5-S6] 旧固定比例分摊(FinAmoebaAllocation)已废弃删除；数据点直接进入视图(件量分摊在多期路径)
        var allocatedPoints = allDataPoints;

        // 10. 按方向筛选（按归并桶比较，兼容模板 Tab 自定义命名）
        if (!string.IsNullOrEmpty(request.Direction))
        {
            allocatedPoints = allocatedPoints
                .Where(p => MapDirectionToBucket(p.Direction) == request.Direction)
                .ToList();
        }

        // 11. 按品牌筛选（严格口径：无品牌归属的数据点同样剔除）
        if (request.BrandCodes != null && request.BrandCodes.Count > 0)
        {
            allocatedPoints = allocatedPoints
                .Where(p => !string.IsNullOrEmpty(p.BrandCode) && request.BrandCodes.Contains(p.BrandCode))
                .ToList();
        }

        // 12. 构建列信息（经营单元或网点）
        var columns = new List<(string Id, string Name)>();
        if (request.ViewMode == "site")
        {
            var siteCodes = allocatedPoints
                .Where(p => !string.IsNullOrEmpty(p.SiteCode))
                .Select(p => p.SiteCode!)
                .Distinct()
                .OrderBy(s => s)
                .ToList();
            columns = siteCodes.Select(s => (s, s)).ToList();
        }
        else
        {
            var targetUnits = units;
            if (request.UnitIds != null && request.UnitIds.Count > 0)
                targetUnits = units.Where(u => request.UnitIds.Contains(u.FID)).ToList();
            columns = targetUnits.Select(u => (u.FID.ToString(), u.FName)).ToList();
        }

        // 13. 生成Excel
        var viewModeLabel = request.ViewMode == "site" ? "网点视角" : "经营单元视角";
        return GenerateExcelBytes(orgName, request.StartDate, request.EndDate, viewModeLabel,
            columns, allocatedPoints, plItems, request.ViewMode);
    }

    /// <summary>
    /// 生成Excel字节数组
    /// </summary>
    private byte[] GenerateExcelBytes(
        string orgName, DateTime startDate, DateTime endDate, string viewModeLabel,
        List<(string Id, string Name)> columns,
        List<DataPoint> dataPoints, List<FinAmoebaPLItem> plItems, string viewMode)
    {
        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("阿米巴经营报表");

        // === 创建样式 ===
        var styles = CreateExcelStyles(workbook, columns.Count);

        int colCount = 2 + columns.Count; // 损益项目 + 合计 + N个单元列
        int rowIdx = 0;

        // --- 第1行：标题 ---
        var titleRow = sheet.CreateRow(rowIdx++);
        titleRow.HeightInPoints = 24;
        var titleCell = titleRow.CreateCell(0);
        titleCell.SetCellValue($"{orgName} 阿米巴经营报表");
        titleCell.CellStyle = styles.Title;
        sheet.AddMergedRegion(new CellRangeAddress(0, 0, 0, colCount - 1));

        // --- 第2行：副标题 ---
        var subtitleRow = sheet.CreateRow(rowIdx++);
        subtitleRow.HeightInPoints = 18;
        var subtitleCell = subtitleRow.CreateCell(0);
        subtitleCell.SetCellValue($"报告期间：{startDate:yyyy-MM-dd} ~ {endDate:yyyy-MM-dd} / {viewModeLabel}");
        subtitleCell.CellStyle = styles.Subtitle;
        sheet.AddMergedRegion(new CellRangeAddress(1, 1, 0, colCount - 1));

        // --- 第3行：空行 ---
        sheet.CreateRow(rowIdx++);

        // --- 第4行：表头 ---
        var headerRow = sheet.CreateRow(rowIdx++);
        var hc0 = headerRow.CreateCell(0);
        hc0.SetCellValue("损益项目");
        hc0.CellStyle = styles.Header;
        var hc1 = headerRow.CreateCell(1);
        hc1.SetCellValue("合计");
        hc1.CellStyle = styles.Header;
        for (int i = 0; i < columns.Count; i++)
        {
            var hc = headerRow.CreateCell(i + 2);
            hc.SetCellValue(columns[i].Name);
            hc.CellStyle = styles.Header;
        }

        // --- 数据行 ---
        // 按模板树结构输出：按 Tab（depth=0 group）分组
        var tabRootsExcel = plItems
            .Where(i => i.FParentId == 0 && i.FNodeRole == "group")
            .OrderBy(i => i.FSort)
            .ToList();
        decimal totalNetProfit = 0;

        foreach (var tabRoot in tabRootsExcel)
        {
            var tabLabel = tabRoot.FItemName;

            // L1: Tab根行
            var dirRow = sheet.CreateRow(rowIdx++);
            var dirCell = dirRow.CreateCell(0);
            dirCell.SetCellValue(tabLabel);
            dirCell.CellStyle = styles.L1Label;
            for (int c = 1; c < colCount; c++)
            {
                var ec = dirRow.CreateCell(c);
                ec.CellStyle = styles.L1Empty;
            }

            // 获取该Tab下所有data节点
            var tabDataItems = GetAllDescendantDataItems(tabRoot, plItems);
            var tabDataItemIds = new HashSet<long>(tabDataItems.Select(pi => pi.FID));
            var tabPoints = dataPoints.Where(p => p.PLItemId != null && tabDataItemIds.Contains(p.PLItemId.Value)).ToList();

            // L2: 所有data项明细
            decimal tabTotal = 0m;
            foreach (var item in tabDataItems)
            {
                var itemPoints = dataPoints.Where(p => p.PLItemId == item.FID).ToList();
                decimal itemTotal = itemPoints.Sum(p => p.Amount);
                tabTotal += itemTotal;

                var itemRow = sheet.CreateRow(rowIdx++);
                var itemCell = itemRow.CreateCell(0);
                itemCell.SetCellValue($"  {item.FItemName}");
                itemCell.CellStyle = styles.L3Label;
                var itemSumCell = itemRow.CreateCell(1);
                itemSumCell.SetCellValue((double)itemTotal);
                itemSumCell.CellStyle = styles.L3NumberBold;
                for (int i = 0; i < columns.Count; i++)
                {
                    var colAmount = GetColumnAmount(itemPoints, columns[i].Id, viewMode);
                    var cc = itemRow.CreateCell(i + 2);
                    cc.SetCellValue((double)colAmount);
                    cc.CellStyle = styles.L3Number;
                }
            }

            totalNetProfit += tabTotal;
        }

        // 经营净利润（最终行）
        WriteSummaryRow(sheet, ref rowIdx, "经营净利润", totalNetProfit, dataPoints, columns, viewMode, styles, isFinal: true);

        // === 列宽 ===
        sheet.SetColumnWidth(0, 32 * 256);
        for (int c = 1; c < colCount; c++)
            sheet.SetColumnWidth(c, 16 * 256);

        // === 冻结 ===
        sheet.CreateFreezePane(1, 4); // 冻结第1列+前4行

        using var ms = new MemoryStream();
        workbook.Write(ms, true);
        return ms.ToArray();
    }

    /// <summary>写分类（收入或成本）：L2汇总行 + 多个L3明细行</summary>
    private decimal WriteCategory(ISheet sheet, ref int rowIdx,
        string categoryLabel, List<FinAmoebaPLItem> items, List<DataPoint> categoryPoints,
        List<(string Id, string Name)> columns, string viewMode, ExcelStyles styles)
    {
        // 计算分类合计
        decimal categoryTotal = categoryPoints.Sum(p => p.Amount);

        // L2: 分类汇总行
        var catRow = sheet.CreateRow(rowIdx++);
        var catCell = catRow.CreateCell(0);
        catCell.SetCellValue(categoryLabel);
        catCell.CellStyle = styles.L2Label;
        // 合计列
        var catSumCell = catRow.CreateCell(1);
        catSumCell.SetCellValue((double)categoryTotal);
        catSumCell.CellStyle = styles.L2NumberBold;
        // 各单元列
        for (int i = 0; i < columns.Count; i++)
        {
            var colAmount = GetColumnAmount(categoryPoints, columns[i].Id, viewMode);
            var cc = catRow.CreateCell(i + 2);
            cc.SetCellValue((double)colAmount);
            cc.CellStyle = styles.L2Number;
        }

        // L3: 具体损益项
        foreach (var item in items)
        {
            var itemPoints = categoryPoints.Where(p => p.PLItemId == item.FID).ToList();
            decimal itemTotal = itemPoints.Sum(p => p.Amount);

            var itemRow = sheet.CreateRow(rowIdx++);
            var itemCell = itemRow.CreateCell(0);
            itemCell.SetCellValue($"  {item.FItemName}"); // 2空格缩进
            itemCell.CellStyle = styles.L3Label;
            // 合计
            var itemSumCell = itemRow.CreateCell(1);
            itemSumCell.SetCellValue((double)itemTotal);
            itemSumCell.CellStyle = styles.L3NumberBold;
            // 各单元列
            for (int i = 0; i < columns.Count; i++)
            {
                var colAmount = GetColumnAmount(itemPoints, columns[i].Id, viewMode);
                var cc = itemRow.CreateCell(i + 2);
                cc.SetCellValue((double)colAmount);
                cc.CellStyle = styles.L3Number;
            }
        }

        // 未匹配到损益项的"其他"
        var unmatchedPoints = categoryPoints.Where(p => p.PLItemId == null || !items.Any(pi => pi.FID == p.PLItemId)).ToList();
        decimal unmatchedTotal = unmatchedPoints.Sum(p => p.Amount);
        if (unmatchedTotal != 0)
        {
            var otherRow = sheet.CreateRow(rowIdx++);
            var otherCell = otherRow.CreateCell(0);
            otherCell.SetCellValue("  其他");
            otherCell.CellStyle = styles.L3Label;
            var otherSumCell = otherRow.CreateCell(1);
            otherSumCell.SetCellValue((double)unmatchedTotal);
            otherSumCell.CellStyle = styles.L3NumberBold;
            for (int i = 0; i < columns.Count; i++)
            {
                var colAmount = GetColumnAmount(unmatchedPoints, columns[i].Id, viewMode);
                var cc = otherRow.CreateCell(i + 2);
                cc.SetCellValue((double)colAmount);
                cc.CellStyle = styles.L3Number;
            }
        }

        return categoryTotal;
    }

    /// <summary>写汇总行（毛利/净利润）</summary>
    private void WriteSummaryRow(ISheet sheet, ref int rowIdx,
        string label, decimal totalAmount, List<DataPoint> dirPoints,
        List<(string Id, string Name)> columns, string viewMode, ExcelStyles styles, bool isFinal = false)
    {
        var row = sheet.CreateRow(rowIdx++);
        var labelCell = row.CreateCell(0);
        labelCell.SetCellValue(label);
        labelCell.CellStyle = styles.SummaryLabel;

        var sumCell = row.CreateCell(1);
        sumCell.SetCellValue((double)totalAmount);
        sumCell.CellStyle = styles.SummaryNumberBold;

        for (int i = 0; i < columns.Count; i++)
        {
            // 对于毛利行：该单元的收入 - 成本
            decimal colAmount;
            if (isFinal)
            {
                // 经营净利润 = 所有方向收入 - 所有方向成本
                var colRevenue = GetColumnAmount(dirPoints.Where(p => p.Category == "revenue").ToList(), columns[i].Id, viewMode);
                var colCost = GetColumnAmount(dirPoints.Where(p => p.Category == "cost" || p.Category == "expense" || p.Category == "depreciation").ToList(), columns[i].Id, viewMode);
                colAmount = colRevenue - colCost;
            }
            else
            {
                var colRevenue = GetColumnAmount(dirPoints.Where(p => p.Category == "revenue").ToList(), columns[i].Id, viewMode);
                var colCost = GetColumnAmount(dirPoints.Where(p => p.Category == "cost" || p.Category == "expense" || p.Category == "depreciation").ToList(), columns[i].Id, viewMode);
                colAmount = colRevenue - colCost;
            }
            var cc = row.CreateCell(i + 2);
            cc.SetCellValue((double)colAmount);
            cc.CellStyle = styles.SummaryNumber;
        }
    }

    /// <summary>获取某列（单元/网点）的金额合计</summary>
    private static decimal GetColumnAmount(List<DataPoint> points, string columnId, string viewMode)
    {
        if (viewMode == "site")
            return points.Where(p => p.SiteCode == columnId).Sum(p => p.Amount);
        else
            return points.Where(p => p.MappedUnitId.HasValue && p.MappedUnitId.Value.ToString() == columnId).Sum(p => p.Amount);
    }

    /// <summary>创建所有Excel样式</summary>
    private static ExcelStyles CreateExcelStyles(XSSFWorkbook workbook, int columnCount)
    {
        var styles = new ExcelStyles();

        // 数值格式
        var numFormat = workbook.CreateDataFormat().GetFormat("#,##0.00");

        // --- 标题样式 ---
        styles.Title = workbook.CreateCellStyle();
        var titleFont = workbook.CreateFont();
        titleFont.FontHeightInPoints = 16;
        titleFont.IsBold = true;
        styles.Title.SetFont(titleFont);
        styles.Title.Alignment = HorizontalAlignment.Center;
        styles.Title.VerticalAlignment = VerticalAlignment.Center;

        // --- 副标题 ---
        styles.Subtitle = workbook.CreateCellStyle();
        var subtitleFont = workbook.CreateFont();
        subtitleFont.FontHeightInPoints = 11;
        subtitleFont.Color = IndexedColors.Grey50Percent.Index;
        styles.Subtitle.SetFont(subtitleFont);
        styles.Subtitle.Alignment = HorizontalAlignment.Center;
        styles.Subtitle.VerticalAlignment = VerticalAlignment.Center;

        // --- 表头样式 ---
        styles.Header = workbook.CreateCellStyle();
        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        styles.Header.SetFont(headerFont);
        styles.Header.Alignment = HorizontalAlignment.Center;
        styles.Header.VerticalAlignment = VerticalAlignment.Center;
        styles.Header.FillForegroundColor = IndexedColors.Grey25Percent.Index;
        styles.Header.FillPattern = FillPattern.SolidForeground;
        SetThinBorder(styles.Header);

        // --- L1 方向行（深蓝底+白字加粗）---
        styles.L1Label = workbook.CreateCellStyle();
        var l1Font = workbook.CreateFont();
        l1Font.IsBold = true;
        l1Font.Color = IndexedColors.White.Index;
        styles.L1Label.SetFont(l1Font);
        ((XSSFCellStyle)styles.L1Label).SetFillForegroundColor(new XSSFColor(new byte[] { 31, 78, 121 }));
        styles.L1Label.FillPattern = FillPattern.SolidForeground;
        styles.L1Label.Alignment = HorizontalAlignment.Left;
        SetThinBorder(styles.L1Label);

        styles.L1Empty = workbook.CreateCellStyle();
        ((XSSFCellStyle)styles.L1Empty).SetFillForegroundColor(new XSSFColor(new byte[] { 31, 78, 121 }));
        styles.L1Empty.FillPattern = FillPattern.SolidForeground;
        SetThinBorder(styles.L1Empty);

        // --- L2 分类汇总（浅蓝底+深色加粗）---
        var l2Font = workbook.CreateFont();
        l2Font.IsBold = true;

        styles.L2Label = workbook.CreateCellStyle();
        styles.L2Label.SetFont(l2Font);
        ((XSSFCellStyle)styles.L2Label).SetFillForegroundColor(new XSSFColor(new byte[] { 189, 215, 238 }));
        styles.L2Label.FillPattern = FillPattern.SolidForeground;
        styles.L2Label.Alignment = HorizontalAlignment.Left;
        SetThinBorder(styles.L2Label);

        styles.L2Number = workbook.CreateCellStyle();
        ((XSSFCellStyle)styles.L2Number).SetFillForegroundColor(new XSSFColor(new byte[] { 189, 215, 238 }));
        styles.L2Number.FillPattern = FillPattern.SolidForeground;
        styles.L2Number.Alignment = HorizontalAlignment.Right;
        styles.L2Number.DataFormat = numFormat;
        SetThinBorder(styles.L2Number);

        styles.L2NumberBold = workbook.CreateCellStyle();
        styles.L2NumberBold.SetFont(l2Font);
        ((XSSFCellStyle)styles.L2NumberBold).SetFillForegroundColor(new XSSFColor(new byte[] { 189, 215, 238 }));
        styles.L2NumberBold.FillPattern = FillPattern.SolidForeground;
        styles.L2NumberBold.Alignment = HorizontalAlignment.Right;
        styles.L2NumberBold.DataFormat = numFormat;
        SetThinBorder(styles.L2NumberBold);

        // --- L3 具体项（白底）---
        styles.L3Label = workbook.CreateCellStyle();
        styles.L3Label.Alignment = HorizontalAlignment.Left;
        SetThinBorder(styles.L3Label);

        styles.L3Number = workbook.CreateCellStyle();
        styles.L3Number.Alignment = HorizontalAlignment.Right;
        styles.L3Number.DataFormat = numFormat;
        SetThinBorder(styles.L3Number);

        var boldFont = workbook.CreateFont();
        boldFont.IsBold = true;
        styles.L3NumberBold = workbook.CreateCellStyle();
        styles.L3NumberBold.SetFont(boldFont);
        styles.L3NumberBold.Alignment = HorizontalAlignment.Right;
        styles.L3NumberBold.DataFormat = numFormat;
        SetThinBorder(styles.L3NumberBold);

        // --- 汇总行（浅橙底+加粗+上边框）---
        var summaryFont = workbook.CreateFont();
        summaryFont.IsBold = true;

        styles.SummaryLabel = workbook.CreateCellStyle();
        styles.SummaryLabel.SetFont(summaryFont);
        ((XSSFCellStyle)styles.SummaryLabel).SetFillForegroundColor(new XSSFColor(new byte[] { 252, 228, 214 }));
        styles.SummaryLabel.FillPattern = FillPattern.SolidForeground;
        styles.SummaryLabel.Alignment = HorizontalAlignment.Left;
        SetThinBorder(styles.SummaryLabel);
        styles.SummaryLabel.BorderTop = BorderStyle.Medium;

        styles.SummaryNumber = workbook.CreateCellStyle();
        ((XSSFCellStyle)styles.SummaryNumber).SetFillForegroundColor(new XSSFColor(new byte[] { 252, 228, 214 }));
        styles.SummaryNumber.FillPattern = FillPattern.SolidForeground;
        styles.SummaryNumber.Alignment = HorizontalAlignment.Right;
        styles.SummaryNumber.DataFormat = numFormat;
        SetThinBorder(styles.SummaryNumber);
        styles.SummaryNumber.BorderTop = BorderStyle.Medium;

        styles.SummaryNumberBold = workbook.CreateCellStyle();
        styles.SummaryNumberBold.SetFont(summaryFont);
        ((XSSFCellStyle)styles.SummaryNumberBold).SetFillForegroundColor(new XSSFColor(new byte[] { 252, 228, 214 }));
        styles.SummaryNumberBold.FillPattern = FillPattern.SolidForeground;
        styles.SummaryNumberBold.Alignment = HorizontalAlignment.Right;
        styles.SummaryNumberBold.DataFormat = numFormat;
        SetThinBorder(styles.SummaryNumberBold);
        styles.SummaryNumberBold.BorderTop = BorderStyle.Medium;

        return styles;
    }

    private static void SetThinBorder(ICellStyle style)
    {
        style.BorderTop = BorderStyle.Thin;
        style.BorderBottom = BorderStyle.Thin;
        style.BorderLeft = BorderStyle.Thin;
        style.BorderRight = BorderStyle.Thin;
    }

    private class ExcelStyles
    {
        public ICellStyle Title { get; set; } = null!;
        public ICellStyle Subtitle { get; set; } = null!;
        public ICellStyle Header { get; set; } = null!;
        public ICellStyle L1Label { get; set; } = null!;
        public ICellStyle L1Empty { get; set; } = null!;
        public ICellStyle L2Label { get; set; } = null!;
        public ICellStyle L2Number { get; set; } = null!;
        public ICellStyle L2NumberBold { get; set; } = null!;
        public ICellStyle L3Label { get; set; } = null!;
        public ICellStyle L3Number { get; set; } = null!;
        public ICellStyle L3NumberBold { get; set; } = null!;
        public ICellStyle SummaryLabel { get; set; } = null!;
        public ICellStyle SummaryNumber { get; set; } = null!;
        public ICellStyle SummaryNumberBold { get; set; } = null!;
    }

    #endregion

    #region 数据聚合方法

    /// <summary>
    /// 从EXP计费结果聚合出港收入和业务指标
    /// </summary>
    private async Task<List<DataPoint>> AggregateBillingRevenue(
        DateTime startDate, DateTime endDate, long orgId, List<FinAmoebaPLItem> plItems,
        List<FinAmoebaMappingRule>? mappingRules = null, List<long>? unitIds = null,
        List<string>? siteCodes = null)
    {
        // 设定查询的网点范围：优先用 siteCodes 直接过滤，其次通过 unitIds 反查
        List<string>? targetSiteCodes = null;
        if (siteCodes != null && siteCodes.Count > 0)
        {
            targetSiteCodes = siteCodes.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
            if (!targetSiteCodes.Any())
                return new List<DataPoint>();
        }
        else if (unitIds != null && unitIds.Count > 0 && mappingRules != null)
        {
            targetSiteCodes = mappingRules
                .Where(r => unitIds.Contains(r.FUnitId) && (r.FDataSourceType == 1 || r.FDataSourceType == 0))
                .Where(r => !string.IsNullOrEmpty(r.FSiteCode))
                .Select(r => r.FSiteCode!)
                .Distinct()
                .ToList();

            if (!targetSiteCodes.Any())
                return new List<DataPoint>();
        }

        // 构建 SQL。额外提取 F业务对象编号以支持损益项级辅助核算过滤（business_object 类型）
        // 不限 F计算状态，按其分组，匹配时根据损益项 scope 决定是否只取已计价数据
        // 注意：SUM 在无数据/全 NULL 时返回 NULL，而 BillingAggRow.TotalAmount/TotalWeight 为 decimal 非空，
        // 必须用 ISNULL 包装避免 SqlNullValueException。
        var sql = @"SELECT 
                    [F归属网点编号] AS SiteCode,
                    [F品牌编码] AS BrandCode,
                    [F业务对象编号] AS BusinessObjectCode,
                    [F计算状态] AS CalcStatus,
                    ISNULL(SUM([F应收金额]), 0) AS TotalAmount,
                    COUNT(*) AS WaybillCount,
                    ISNULL(SUM([F计费重量]), 0) AS TotalWeight
                FROM [EXP出港运单_计费结果]
                WHERE [F运单日期] >= {0} AND [F运单日期] < {1}
                    AND [F组织ID] = {2}";

        // 半开区间 [起, 末日+1)：F运单日期可能带时分(import 保留)，闭区间 <=末日(0点) 会把末日带时分的运单漏掉；
        // 与 AggregateVoucherData 的 < end.AddDays(1) 口径一致。[批次5-S3] day 粒度下尤为关键(否则整日全丢)。
        var parameters = new List<object> { startDate, endDate.AddDays(1), orgId };

        if (targetSiteCodes != null)
        {
            var inClause = string.Join(",", targetSiteCodes.Select((_, i) => $"{{{i + 3}}}"));
            sql += $" AND [F归属网点编号] IN ({inClause})";
            parameters.AddRange(targetSiteCodes.Cast<object>());
        }

        sql += " GROUP BY [F归属网点编号], [F品牌编码], [F业务对象编号], [F计算状态]";

        var results = await _dbContext.Database
            .SqlQueryRaw<BillingAggRow>(sql, parameters.ToArray())
            .ToListAsync();

        // billing 数据不再独占归给首个 billing 叶子项，统一走 MatchDataPointsToPLItems 的三层过滤逻辑。
        return results.Select(r =>
        {
            var siteCode = r.SiteCode?.Trim();
            var businessObjectCode = r.BusinessObjectCode?.Trim();
            var auxValues = new Dictionary<string, string>();
            // 方案B 源打标(A1)：出港运单计费结果天然出港，无条件写 business_direction=OUT，
            // 供损益项 business_direction filter 命中；DataPoint.Direction 维持中文不动(批次3 再统一为 OUT/IN/CMB)。
            auxValues[AuxTypes.BusinessDirection] = BusinessDirection.Outbound;
            if (!string.IsNullOrEmpty(siteCode))
                auxValues["outlet"] = siteCode;
            if (!string.IsNullOrEmpty(businessObjectCode))
                auxValues["business_object"] = businessObjectCode;

            return new DataPoint
            {
                Source = "billing",
                SiteCode = siteCode,
                BrandCode = r.BrandCode?.Trim(),
                Direction = "出港",
                Amount = r.TotalAmount,
                Category = "revenue",
                WaybillCount = r.WaybillCount,
                Weight = r.TotalWeight,
                IsPriced = (r.CalcStatus == 1),
                AuxValues = auxValues.Count > 0 ? auxValues : null
            };
        }).ToList();
    }

    /// <summary>
    /// 从 STG申通派件日明细 聚合 deliver（派件）件量（设计 spec 派件量接入 §B1）。平行 AggregateBillingRevenue：
    /// 参数化 SQL、半开区间 [起, 末日+1)、按组织(不按账套，STG 按组织隔离)；
    /// 过滤列用系统列 [F归属网点编号]（OutletResolver 规范化，与 billing dp.SiteCode / scope.Outlets 同源）。
    /// 取数列 [F基础派费收费件量] = 派件量。原始 SQL，InMemory 不可测，靠 SQL Server 冒烟覆盖。
    /// SUM 在无数据/全 NULL 时返回 NULL，用 ISNULL 包装为 0 避免 SqlNullValueException。
    /// v1 仅按 scope.Outlets（网点）裁；Direction/Projects 维度不下推（见 spec 派件量接入 §风险：方向子报表 deliver 基数口径 v1 未定）。
    /// </summary>
    private async Task<long> AggregateDeliverVolume(
        DateTime startDate, DateTime endDate, long orgId, AmoebaReportScope? scope)
    {
        var outlets = scope?.Outlets?.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

        var sql = @"SELECT ISNULL(SUM([F基础派费收费件量]), 0) AS [Value]
                    FROM [STG申通派件日明细]
                    WHERE [F结算日期] >= {0} AND [F结算日期] < {1}
                        AND [FOrgId] = {2} AND [FIsRevoked] = 0";
        // 半开区间 [起, 末日+1)：与 AggregateBillingRevenue / AggregateVoucherData 口径一致
        var parameters = new List<object> { startDate, endDate.AddDays(1), orgId };

        if (outlets is { Count: > 0 })
        {
            var inClause = string.Join(",", outlets.Select((_, i) => $"{{{i + 3}}}"));
            sql += $" AND [F归属网点编号] IN ({inClause})";
            parameters.AddRange(outlets.Cast<object>());
        }

        return await _dbContext.Database
            .SqlQueryRaw<long>(sql, parameters.ToArray())
            .FirstAsync();
    }

    /// <summary>
    /// 从凭证分录聚合（收入+成本+费用）
    /// </summary>
    private async Task<List<DataPoint>> AggregateVoucherData(DateTime startDate, DateTime endDate, long orgId, long accountSetId)
    {
        // 查询日期范围内已过账凭证分录
        var entries = await (from e in _voucherEntryRepository.Query()
                            join v in _voucherRepository.Query()
                                // 含已审核未过账(FStatus>=1, 用户口径)；排除红冲/作废(P1-5)
                                .Where(v => v.FAccountSetId == accountSetId && v.FStatus >= 1 && !v.FIsRevoked)
                            on e.FVoucherId equals v.FID
                            // 半开区间 [月初, 次月初)：避免月末当天带时分的凭证被 <=endDate(月末0点) 漏掉(P1-6)
                            where v.FDate >= startDate && v.FDate < endDate.AddDays(1)
                            select new
                            {
                                Entry = e,
                                VoucherSource = v.FSource,
                                VoucherDate = v.FDate
                            }).ToListAsync();

        // 获取所有科目
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();
        var accountDict = accounts.ToDictionary(a => a.FID, a => a);

        var dataPoints = new List<DataPoint>();

        foreach (var item in entries)
        {
            if (!accountDict.TryGetValue(item.Entry.FAccountId, out var account))
                continue;

            var category = GetAccountCategory(account);
            if (string.IsNullOrEmpty(category))
                continue;

            // 去重规则：对于品牌导入/计费生成的凭证，只排除收入类科目金额
            bool isExcludedSource = !string.IsNullOrEmpty(item.VoucherSource)
                && ExcludedRevenueVoucherSources.Any(s => item.VoucherSource.Contains(s));

            if (isExcludedSource && category == "revenue")
                continue;

            // 计算金额
            decimal amount = CalculateAmount(item.Entry, account);
            if (amount == 0) continue;

            // 解析辅助核算获取经营单元ID和品牌信息
            long? businessUnitId = null;
            string? siteCode = null;
            string? brandCode = null;
            string? brandName = null;
            Dictionary<string, string>? auxValues = null;

            if (!string.IsNullOrEmpty(item.Entry.FAuxiliaryJson))
            {
                var auxItems = ParseAuxiliaryJson(item.Entry.FAuxiliaryJson);
                if (auxItems.Count > 0)
                    auxValues = new Dictionary<string, string>();
                foreach (var aux in auxItems)
                {
                    // 全量入字典，供损益项级辅助核算过滤使用
                    if (!string.IsNullOrEmpty(aux.Type) && !string.IsNullOrEmpty(aux.Code))
                        auxValues![aux.Type] = aux.Code;

                    switch (aux.Type)
                    {
                        case "business_unit":
                            businessUnitId = aux.Id;
                            break;
                        case "express_brand":
                            brandCode = aux.Code;
                            brandName = aux.Name;
                            break;
                        case "outlet":
                            // 网点辅助核算：以辅助项 code 作为 SiteCode（与 EXP快递网点.F编号 保持一致）
                            siteCode = aux.Code;
                            break;
                    }
                }
            }

            // 从FSource提取品牌（辅助核算中没有时兜底）
            if (string.IsNullOrEmpty(brandCode))
                brandCode = ExtractBrandCode(item.VoucherSource);

            dataPoints.Add(new DataPoint
            {
                Source = "voucher",
                SiteCode = siteCode,
                BrandCode = brandCode,
                BrandName = brandName,
                BusinessUnitId = businessUnitId,
                Direction = null, // 待分类
                AccountId = account.FID,
                AccountCode = account.FCode,
                Summary = item.Entry.FSummary,
                Amount = amount,
                Category = category,
                VoucherEntryId = item.Entry.FID,
                AuxValues = auxValues
            });
        }

        return dataPoints;
    }

    /// <summary>
    /// 资产折旧聚合
    /// </summary>
    internal async Task<List<DataPoint>> AggregateDepreciation(DateTime startDate, DateTime endDate, long orgId, long accountSetId)
    {
        var query = _dbContext.Set<FinAssetCard>()
            .Where(a => a.FOrgId == orgId && a.FStatus == 1);

        if (accountSetId > 0)
            query = query.Where(a => a.FAccountSetId == accountSetId);

        var assetCards = await query.ToListAsync();

        if (!assetCards.Any())
            return new List<DataPoint>();

        var dataPoints = new List<DataPoint>();

        foreach (var card in assetCards)
        {
            // 逐卡取整到分，与折旧下钻 GetDepreciationDrillDownAsync 保持同一取整口径：
            // 多卡片时"先逐卡取整后求和"才能让下钻合计与报表数字逐分一致（折旧按卡逐张过账，
            // 聚合端若累加未取整金额，累计舍入误差会与下钻差几分钱）。
            var periodDepreciation = Math.Round(CalculatePeriodDepreciation(card, startDate, endDate), 2);
            if (periodDepreciation <= 0) continue;

            // 从 F备注 解析借方科目编码（格式: "借:560102/部门xx | 贷:1602/部门xx"）
            var accountCode = ExtractDepreciationDebitAccountCode(card.FRemark);

            dataPoints.Add(new DataPoint
            {
                Source = "depreciation",
                Direction = null, // 不硬编码，让 ClassifyDirection 通过 Layer 2 匹配决定
                Amount = periodDepreciation,
                Category = "depreciation",
                AccountCode = accountCode ?? "560102" // 兜底默认值
            });
        }

        return dataPoints;
    }

    /// <summary>
    /// 计算资产卡片在 [startDate, endDate] 内应计提的折旧额。
    /// 月折旧 = (原值 - 残值) / 使用寿命月数；按自然月天数比例分摊（替代 /30 近似），
    /// 计提窗口 = [开始折旧日期(或入账日期), 起始+寿命月数)，窗口外（未启用/已提足）不计提。
    /// </summary>
    private static decimal CalculatePeriodDepreciation(FinAssetCard card, DateTime startDate, DateTime endDate)
    {
        if (card.FUsefulLife == null || card.FUsefulLife <= 0) return 0m;
        if (endDate < startDate) return 0m;

        var residualRate = card.FResidualRate ?? 0;
        var monthly = (card.FOriginalValue * (1 - residualRate / 100)) / card.FUsefulLife.Value;
        if (monthly <= 0) return 0m;

        // 计提窗口；历史数据缺日期（DateTime.MinValue）时视为始终在窗口内，与旧行为兼容
        var depStart = (card.FStartDepreciationDate ?? card.FEntryDate).Date;
        var hasWindow = depStart > DateTime.MinValue.Date;
        var depEnd = hasWindow ? depStart.AddMonths(card.FUsefulLife.Value) : DateTime.MaxValue.Date; // 提足后停止（不含当日）

        var total = 0m;
        var monthCursor = new DateTime(startDate.Year, startDate.Month, 1);
        var rangeEnd = endDate.Date;
        while (monthCursor <= rangeEnd)
        {
            var monthEnd = monthCursor.AddMonths(1).AddDays(-1);
            var overlapStart = Max(Max(monthCursor, startDate.Date), hasWindow ? depStart : monthCursor);
            var overlapEndExclusive = Min(Min(monthEnd, rangeEnd).AddDays(1), depEnd);
            var overlapDays = (overlapEndExclusive - overlapStart).Days;
            if (overlapDays > 0)
            {
                var daysInMonth = DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month);
                total += monthly * overlapDays / daysInMonth;
            }
            monthCursor = monthCursor.AddMonths(1);
        }
        return total;

        static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
        static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    }

    /// <summary>
    /// [批次5-S3] 按粒度构建一期的数据点集合（设计 §5.1 裁决C1 + 当月例外）。
    /// billing 两类粒度共享(按日期可裁)；台账(月/季/年)叠加 voucher+depreciation，仅当期间未结(当月/未来)
    /// 再叠加 estimate 临时填充、已结过往期改用凭证；估算(日/周)叠加 estimate(opreport，按期间键取)、
    /// 不取 voucher(按月过账)/depreciation(按月计提)。两源产出同形 DataPoint，下游 Match/scope/分摊复用。
    /// </summary>
    private async Task<List<DataPoint>> BuildPeriodDataPoints(
        string period, string granularity, long orgId, long accountSetId, long templateId,
        List<FinAmoebaPLItem> plItems, List<FinAmoebaMappingRule> mappingRules)
    {
        var (startDate, endDate) = PeriodToDateRange(period, granularity);
        // 当月例外：已结台账期(末日<今天)用凭证实时聚合，未结(当月/未来)叠加估值临时填充
        var src = SelectSources(granularity, periodClosed: endDate < DateTime.Today);
        var all = new List<DataPoint>();
        if (src.Billing)
            all.AddRange(await AggregateBillingRevenue(startDate, endDate, orgId, plItems, mappingRules, null, null));
        if (src.Voucher)
            all.AddRange(await AggregateVoucherData(startDate, endDate, orgId, accountSetId));
        if (src.Depreciation)
            all.AddRange(await AggregateDepreciation(startDate, endDate, orgId, accountSetId));
        if (src.Estimate)
            all.AddRange(await AggregateEstimateData(templateId, orgId, BuildPeriodKey(period, granularity)));
        return all;
    }

    /// <summary>
    /// 暂估数据聚合：将 FinAmoebaManualData 中 FDataType=estimate 的记录转为 DataPoint。
    /// 暂估数据不绑定 PLItem，通过科目编码 + 辅助核算与凭证数据共享独占匹配管道。
    /// [批次5-S3] 按期间键(FPeriodKey)过滤——支持多粒度估值；当月月报取 'M:'+期间 的估值，已结月报不取(C1)。
    /// </summary>
    private async Task<List<DataPoint>> AggregateEstimateData(
        long templateId, long orgId, string periodKey)
    {
        if (templateId <= 0 || orgId <= 0 || string.IsNullOrEmpty(periodKey))
            return new List<DataPoint>();

        var estimates = await _dbContext.Set<FinAmoebaManualData>()
            .Where(m => m.FTemplateId == templateId
                     && m.FOrgId == orgId
                     && m.FDataType == "estimate"
                     && m.FPeriodKey == periodKey)
            .ToListAsync();

        var dataPoints = new List<DataPoint>();
        foreach (var e in estimates)
        {
            var auxValues = ParseEstimateAuxJson(e.FAuxiliaryJson);
            dataPoints.Add(new DataPoint
            {
                Source = "estimate",
                AccountCode = e.FAccountCode,
                Amount = e.FAmount,
                Category = "",
                // 方案B 源打标(F1)：估值带的 business_direction(OUT/IN/CMB)回填 Direction，与 voucher 同口径，
                // 使 business_direction filter 经 AuxiliaryMatches 命中；无则 null。
                Direction = auxValues.GetValueOrDefault("business_direction"),
                AuxValues = auxValues.Count > 0 ? auxValues : null,
                SiteCode = ExtractAuxCode(auxValues, "outlet"),
                BrandCode = ExtractAuxCode(auxValues, "express_brand"),
            });
        }
        return dataPoints;
    }

    /// <summary>
    /// 解析暂估辅助核算 JSON：格式与凭证分录相同 [{"type":"outlet","code":"SC001","name":"城区"}]
    /// 返回 type -> code 的字典。复用现有 ParseAuxiliaryJson 逻辑。
    /// </summary>
    private static Dictionary<string, string> ParseEstimateAuxJson(string? json)
    {
        var dict = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(json)) return dict;
        var items = ParseAuxiliaryJson(json);
        foreach (var aux in items)
        {
            if (!string.IsNullOrEmpty(aux.Type) && !string.IsNullOrEmpty(aux.Code))
                dict[aux.Type] = aux.Code;
        }
        return dict;
    }

    /// <summary>从已解析的辅助核算字典中提取指定类型的 code，无则返回 null</summary>
    private static string? ExtractAuxCode(Dictionary<string, string> auxValues, string auxType)
    {
        if (auxValues == null || string.IsNullOrEmpty(auxType)) return null;
        return auxValues.TryGetValue(auxType, out var code) ? code : null;
    }

    #endregion

    #region 暂估数据 CRUD

    /// <summary>查询某模板某组织某期间的暂估数据列表</summary>
    public async Task<List<ManualDataDto>> GetEstimateDataAsync(long templateId, long orgId, string period)
    {
        return await _dbContext.Set<FinAmoebaManualData>()
            .Where(m => m.FTemplateId == templateId
                     && m.FOrgId == orgId
                     && m.FDataType == "estimate"
                     && m.FPeriod == period)
            .Select(m => new ManualDataDto
            {
                Id = m.FID,
                PLItemId = m.FPLItemId,
                Amount = m.FAmount,
                PerUnitValue = m.FPerUnitValue,
                TemplateId = m.FTemplateId,
                OrgId = m.FOrgId,
                Period = m.FPeriod,
                DataType = m.FDataType,
                AccountCode = m.FAccountCode,
                AuxiliaryJson = m.FAuxiliaryJson,
            })
            .ToListAsync();
    }

    /// <summary>
    /// UPSERT 单条暂估数据：按 (TemplateId, OrgId, Period, AccountCode, AuxiliaryJson) 定位现有记录，
    /// 存在则覆盖金额，不存在则插入。
    /// </summary>
    public async Task SaveEstimateDataAsync(ManualDataDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        var auxJson = dto.AuxiliaryJson ?? string.Empty;
        var accountCode = dto.AccountCode ?? string.Empty;

        var existing = await _dbContext.Set<FinAmoebaManualData>()
            .FirstOrDefaultAsync(m => m.FTemplateId == dto.TemplateId
                                   && m.FOrgId == dto.OrgId
                                   && m.FPeriod == dto.Period
                                   && m.FDataType == "estimate"
                                   && m.FAccountCode == accountCode
                                   && (m.FAuxiliaryJson ?? string.Empty) == auxJson);

        if (existing != null)
        {
            existing.FAmount = dto.Amount;
            existing.FPerUnitValue = dto.PerUnitValue;
            existing.FPeriodKey = BuildPeriodKey(dto.Period);   // [批次5-S3] 自愈：补/纠正期间键(幂等)
            existing.FUpdatedTime = DateTime.Now;
        }
        else
        {
            _dbContext.Set<FinAmoebaManualData>().Add(new FinAmoebaManualData
            {
                FTemplateId = dto.TemplateId,
                FPLItemId = dto.PLItemId,
                FOrgId = dto.OrgId,
                FPeriod = dto.Period,
                FPeriodKey = BuildPeriodKey(dto.Period),   // [批次5-S3] 录入 UI 当前为月度
                FAmount = dto.Amount,
                FPerUnitValue = dto.PerUnitValue,
                FDataType = "estimate",
                FAccountCode = accountCode,
                FAuxiliaryJson = auxJson,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now,
            });
        }
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>按主键 ID 删除一条暂估数据</summary>
    public async Task DeleteEstimateDataAsync(long id, long orgId = 0)
    {
        var query = _dbContext.Set<FinAmoebaManualData>()
            .Where(m => m.FID == id && m.FDataType == "estimate");
        if (orgId > 0)
        {
            query = query.Where(m => m.FOrgId == orgId);
        }

        var entity = await query.FirstOrDefaultAsync();
        if (entity == null) return;
        _dbContext.Set<FinAmoebaManualData>().Remove(entity);
        await _dbContext.SaveChangesAsync();
    }

    #endregion

    #region 方向分类

    /// <summary>
    /// 多层级方向分类（基于新模型，通过匹配 PLItem 后查找其 Tab 祖先名称作为方向）
    /// </summary>
    private string ClassifyDirection(DataPoint point, List<FinAmoebaPLItem> plItems, List<FinAmoebaManualClassify> manualClassifications)
    {
        // 层级1：数据源天然归属
        if (point.Source == "billing")
            return "出港";

        // depreciation 源：默认方向为综合，但继续走 Layer 2 尝试匹配 PLItem
        string? depreciationDefaultDirection = null;
        if (point.Source == "depreciation")
            depreciationDefaultDirection = "综合";

        // 层级2：科目编码前缀匹配（最长前缀优先，从损益项的F关联科目JSON中查找）
        if (!string.IsNullOrEmpty(point.AccountCode))
        {
            FinAmoebaPLItem? bestMatch = null;
            int bestMatchLength = 0;

            foreach (var plItem in plItems.Where(i => !string.IsNullOrEmpty(i.FRelatedAccountsJson)
                && i.FNodeRole == "data"))
            {
                var accountCodes = ParseAccountCodePrefixes(plItem.FRelatedAccountsJson);
                foreach (var code in accountCodes)
                {
                    if (point.AccountCode.StartsWith(code) && code.Length > bestMatchLength)
                    {
                        bestMatch = plItem;
                        bestMatchLength = code.Length;
                    }
                }
            }

            if (bestMatch != null)
            {
                point.PLItemId = bestMatch.FID;
                // 查找 Tab 祖先名称作为方向
                var tabAncestorName = GetTabAncestorName(bestMatch, plItems);
                if (!string.IsNullOrEmpty(tabAncestorName))
                    return tabAncestorName;
            }
        }

        // 层级3：摘要关键词匹配
        if (!string.IsNullOrEmpty(point.Summary))
        {
            foreach (var plItem in plItems.Where(i => !string.IsNullOrEmpty(i.FSummaryKeywordsJson)
                && i.FNodeRole == "data"))
            {
                try
                {
                    var keywords = JsonSerializer.Deserialize<List<string>>(plItem.FSummaryKeywordsJson!);
                    if (keywords != null && keywords.Any(kw => point.Summary.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    {
                        point.PLItemId = plItem.FID;
                        var tabAncestorName = GetTabAncestorName(plItem, plItems);
                        if (!string.IsNullOrEmpty(tabAncestorName))
                            return tabAncestorName;
                    }
                }
                catch { }
            }
        }

        // 层级4：人工分类结果
        if (point.VoucherEntryId.HasValue)
        {
            var manual = manualClassifications.FirstOrDefault(m => m.FVoucherEntryId == point.VoucherEntryId.Value);
            if (manual != null)
            {
                point.PLItemId = manual.FPLItemId;
                var matchedItem = plItems.FirstOrDefault(i => i.FID == manual.FPLItemId);
                if (matchedItem != null)
                {
                    var tabAncestorName = GetTabAncestorName(matchedItem, plItems);
                    if (!string.IsNullOrEmpty(tabAncestorName))
                        return tabAncestorName;
                }
            }
        }

        // 层级5：未分类
        if (depreciationDefaultDirection != null)
            return depreciationDefaultDirection;

        return "综合";
    }

    /// <summary>获取节点的 Tab 祖先名称（向上追溯到 FParentId==0 的 group 节点）</summary>
    private static string? GetTabAncestorName(FinAmoebaPLItem item, List<FinAmoebaPLItem> allItems)
    {
        var itemDict = allItems.ToDictionary(i => i.FID);
        var current = item;
        var visited = new HashSet<long> { current.FID };
        while (current.FParentId != 0 && itemDict.ContainsKey(current.FParentId))
        {
            current = itemDict[current.FParentId];
            if (!visited.Add(current.FID)) return null; // 历史脏数据成环时终止，避免请求挂死
        }
        if (current.FParentId == 0 && current.FNodeRole == "group")
            return current.FItemName;
        return null;
    }

    /// <summary>获取指定根节点下所有 data 后代节点</summary>
    private static List<FinAmoebaPLItem> GetAllDescendantDataItems(FinAmoebaPLItem root, List<FinAmoebaPLItem> allItems)
    {
        var result = new List<FinAmoebaPLItem>();
        var stack = new Stack<FinAmoebaPLItem>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var children = allItems.Where(i => i.FParentId == current.FID).OrderBy(i => i.FSort).ToList();
            foreach (var child in children)
            {
                if (child.FNodeRole == "data")
                    result.Add(child);
                else if (child.FNodeRole == "group")
                    stack.Push(child);
            }
        }
        return result;
    }

    #endregion

    #region 映射逻辑

    /// <summary>
    /// 映射到经营单元
    /// </summary>
    private long? MapToUnit(DataPoint point, List<FinAmoebaMappingRule> rules)
    {
        // 凭证数据：优先使用辅助核算中的经营单元ID
        if (point.Source == "voucher" && point.BusinessUnitId.HasValue)
            return point.BusinessUnitId;

        // 计费/折旧数据：继续使用映射规则
        int dataSourceType = point.Source switch
        {
            "billing" => 1,
            "voucher" => 2,
            "depreciation" => 3,
            _ => 0
        };

        var matchedRules = rules
            .Where(r => r.FDataSourceType == dataSourceType || r.FDataSourceType == 0)
            .Where(r => string.IsNullOrEmpty(r.FSiteCode) || r.FSiteCode == point.SiteCode)
            .Where(r => string.IsNullOrEmpty(r.FBrandCode) || r.FBrandCode == point.BrandCode)
            .Where(r => string.IsNullOrEmpty(r.FDirection) || r.FDirection == point.Direction)
            // 辅助字段条件：规则配置了 FAuxField/FAuxValue 时按数据点辅助核算精确匹配（复用科目级过滤的取值口径）
            .Where(r => string.IsNullOrEmpty(r.FAuxField) || string.IsNullOrEmpty(r.FAuxValue)
                || AuxiliaryMatches(point, new List<AuxFilter> { new() { AuxType = r.FAuxField, Codes = new List<string> { r.FAuxValue } } }))
            .OrderByDescending(r => r.FPriority)
            .ToList();

        return matchedRules.FirstOrDefault()?.FUnitId;
    }

    #endregion

    #region 汇总格式化

    /// <summary>
    /// 递归获取指定损益项的所有后代子项
    /// </summary>
    private List<FinAmoebaPLItem> GetDescendantPLItems(long parentId, List<FinAmoebaPLItem> allItems)
    {
        var result = new List<FinAmoebaPLItem>();
        var children = allItems.Where(i => i.FParentId == parentId).ToList();
        foreach (var child in children)
        {
            result.Add(child);
            result.AddRange(GetDescendantPLItems(child.FID, allItems));
        }
        return result;
    }

    private AmoebaReportResponse BuildUnitViewResponse(List<DataPoint> dataPoints, List<FinAuxiliaryItem> units, AmoebaReportRequest request)
    {
        var response = new AmoebaReportResponse();

        // 筛选单元
        var targetUnits = units;
        if (request.UnitIds != null && request.UnitIds.Count > 0)
        {
            targetUnits = units.Where(u => request.UnitIds.Contains(u.FID)).ToList();
        }

        response.Units = targetUnits.Select(unit =>
        {
            var unitPoints = dataPoints.Where(p => p.MappedUnitId == unit.FID).ToList();

            var unitData = new AmoebaUnitData
            {
                UnitId = unit.FID,
                UnitCode = unit.FCode,
                UnitName = unit.FName,
                ParentId = null  // FinAuxiliaryItem 无父子关系
            };

            // 按方向汇总
            unitData.InboundSubtotal = BuildDirectionMetrics(unitPoints, "进港");
            unitData.OutboundSubtotal = BuildDirectionMetrics(unitPoints, "出港");
            unitData.GeneralSubtotal = BuildDirectionMetrics(unitPoints, "综合");

            // 单元汇总
            unitData.UnitProfit = unitData.InboundSubtotal.Profit + unitData.OutboundSubtotal.Profit + unitData.GeneralSubtotal.Profit;
            unitData.UnitWaybillCount = unitData.InboundSubtotal.WaybillCount + unitData.OutboundSubtotal.WaybillCount + unitData.GeneralSubtotal.WaybillCount;
            unitData.UnitTotalWeight = unitData.InboundSubtotal.TotalWeight + unitData.OutboundSubtotal.TotalWeight + unitData.GeneralSubtotal.TotalWeight;
            unitData.UnitAverageWeight = unitData.UnitWaybillCount > 0 ? unitData.UnitTotalWeight / unitData.UnitWaybillCount : 0;

            var unitRevenue = unitData.InboundSubtotal.Revenue + unitData.OutboundSubtotal.Revenue + unitData.GeneralSubtotal.Revenue;
            var unitCost = unitData.InboundSubtotal.Cost + unitData.OutboundSubtotal.Cost + unitData.GeneralSubtotal.Cost
                + unitData.InboundSubtotal.Expense + unitData.OutboundSubtotal.Expense + unitData.GeneralSubtotal.Expense
                + unitData.InboundSubtotal.AllocatedCost + unitData.OutboundSubtotal.AllocatedCost + unitData.GeneralSubtotal.AllocatedCost;

            unitData.UnitRevenuePerTicket = unitData.UnitWaybillCount > 0 ? unitRevenue / unitData.UnitWaybillCount : 0;
            unitData.UnitCostPerTicket = unitData.UnitWaybillCount > 0 ? unitCost / unitData.UnitWaybillCount : 0;
            unitData.UnitProfitPerTicket = unitData.UnitWaybillCount > 0 ? unitData.UnitProfit / unitData.UnitWaybillCount : 0;

            // 按品牌分组
            var brandGroups = unitPoints
                .Where(p => !string.IsNullOrEmpty(p.BrandCode))
                .GroupBy(p => p.BrandCode!)
                .ToList();

            unitData.Brands = brandGroups.Select(bg => BuildBrandData(bg.Key, bg.ToList())).ToList();

            return unitData;
        }).ToList();

        return response;
    }

    private AmoebaReportResponse BuildSiteViewResponse(List<DataPoint> dataPoints, List<FinAuxiliaryItem> units, AmoebaReportRequest request)
    {
        var response = new AmoebaReportResponse();

        var siteGroups = dataPoints
            .Where(p => !string.IsNullOrEmpty(p.SiteCode))
            .GroupBy(p => p.SiteCode!)
            .ToList();

        if (request.SiteCodes != null && request.SiteCodes.Count > 0)
        {
            siteGroups = siteGroups.Where(g => request.SiteCodes.Contains(g.Key)).ToList();
        }

        var unitDict = units.ToDictionary(u => u.FID, u => u.FName);

        response.Sites = siteGroups.Select(sg =>
        {
            var sitePoints = sg.ToList();
            var siteData = new AmoebaSiteData
            {
                SiteCode = sg.Key,
                SiteName = sg.Key // 后续可从网点表获取名称
            };

            siteData.SiteRevenue = sitePoints.Where(p => p.Category == "revenue").Sum(p => p.Amount);
            siteData.SiteCost = sitePoints.Where(p => p.Category == "cost" || p.Category == "expense" || p.Category == "depreciation").Sum(p => p.Amount);
            siteData.SiteProfit = siteData.SiteRevenue - siteData.SiteCost;
            siteData.SiteWaybillCount = sitePoints.Sum(p => p.WaybillCount);
            siteData.SiteTotalWeight = sitePoints.Sum(p => p.Weight);
            siteData.SiteAverageWeight = siteData.SiteWaybillCount > 0 ? siteData.SiteTotalWeight / siteData.SiteWaybillCount : 0;
            siteData.SiteRevenuePerTicket = siteData.SiteWaybillCount > 0 ? siteData.SiteRevenue / siteData.SiteWaybillCount : 0;
            siteData.SiteCostPerTicket = siteData.SiteWaybillCount > 0 ? siteData.SiteCost / siteData.SiteWaybillCount : 0;
            siteData.SiteProfitPerTicket = siteData.SiteWaybillCount > 0 ? siteData.SiteProfit / siteData.SiteWaybillCount : 0;

            // 按品牌分组
            var brandGroups = sitePoints
                .Where(p => !string.IsNullOrEmpty(p.BrandCode))
                .GroupBy(p => p.BrandCode!)
                .ToList();

            siteData.Brands = brandGroups.Select(bg =>
            {
                var brandData = new AmoebaSiteBrandData
                {
                    BrandCode = bg.Key,
                    BrandName = bg.First().BrandName ?? bg.Key
                };

                var dirGroups = bg.GroupBy(p => p.Direction ?? "综合");
                brandData.Directions = dirGroups.Select(dg =>
                {
                    var mappedUnitId = dg.FirstOrDefault(p => p.MappedUnitId.HasValue)?.MappedUnitId;
                    return new AmoebaSiteDirectionData
                    {
                        Direction = dg.Key,
                        MappedUnitId = mappedUnitId,
                        MappedUnitName = mappedUnitId.HasValue && unitDict.ContainsKey(mappedUnitId.Value) ? unitDict[mappedUnitId.Value] : null,
                        Metrics = BuildDirectionMetrics(dg.ToList(), dg.Key)
                    };
                }).ToList();

                brandData.BrandRevenue = bg.Where(p => p.Category == "revenue").Sum(p => p.Amount);
                brandData.BrandCost = bg.Where(p => p.Category != "revenue").Sum(p => p.Amount);
                brandData.BrandProfit = brandData.BrandRevenue - brandData.BrandCost;
                brandData.BrandWaybillCount = bg.Sum(p => p.WaybillCount);
                var totalWeight = bg.Sum(p => p.Weight);
                brandData.BrandAverageWeight = brandData.BrandWaybillCount > 0 ? totalWeight / brandData.BrandWaybillCount : 0;

                return brandData;
            }).ToList();

            return siteData;
        }).ToList();

        return response;
    }

    private AmoebaOrgSummary BuildOrgSummary(List<DataPoint> dataPoints, long orgId, AmoebaReportRequest request)
    {
        var summary = new AmoebaOrgSummary
        {
            PeriodLabel = $"{request.StartDate:yyyy-MM-dd} ~ {request.EndDate:yyyy-MM-dd}"
        };

        summary.TotalRevenue = dataPoints.Where(p => p.Category == "revenue").Sum(p => p.Amount);
        summary.TotalCost = dataPoints.Where(p => p.Category == "cost").Sum(p => p.Amount);
        summary.TotalExpense = dataPoints.Where(p => p.Category == "expense").Sum(p => p.Amount);
        summary.TotalDepreciation = dataPoints.Where(p => p.Category == "depreciation").Sum(p => p.Amount);
        summary.TotalProfit = summary.TotalRevenue - summary.TotalCost - summary.TotalExpense - summary.TotalDepreciation;
        summary.ProfitRate = summary.TotalRevenue > 0 ? Math.Round(summary.TotalProfit / summary.TotalRevenue * 100, 2) : 0;
        summary.TotalWaybillCount = dataPoints.Sum(p => p.WaybillCount);
        summary.TotalWeight = dataPoints.Sum(p => p.Weight);
        summary.AverageWeight = summary.TotalWaybillCount > 0 ? Math.Round(summary.TotalWeight / summary.TotalWaybillCount, 3) : 0;
        summary.RevenuePerTicket = summary.TotalWaybillCount > 0 ? Math.Round(summary.TotalRevenue / summary.TotalWaybillCount, 2) : 0;
        summary.CostPerTicket = summary.TotalWaybillCount > 0 ? Math.Round((summary.TotalCost + summary.TotalExpense + summary.TotalDepreciation) / summary.TotalWaybillCount, 2) : 0;
        summary.ProfitPerTicket = summary.TotalWaybillCount > 0 ? Math.Round(summary.TotalProfit / summary.TotalWaybillCount, 2) : 0;

        summary.InboundTotal = BuildDirectionMetrics(dataPoints, "进港");
        summary.OutboundTotal = BuildDirectionMetrics(dataPoints, "出港");
        summary.GeneralTotal = BuildDirectionMetrics(dataPoints, "综合");

        return summary;
    }

    /// <summary>
    /// 把数据点的方向（来自模板 Tab 名称，如"出港收入"/"进港业务"）归并到固定三桶：进港/出港/综合。
    /// 不再要求 Tab 严格命名为"进港/出港/综合"——名称包含关键词即可归桶，其余落综合。
    /// </summary>
    private static string MapDirectionToBucket(string? direction)
    {
        if (string.IsNullOrEmpty(direction)) return "综合";
        if (direction.Contains("进港")) return "进港";
        if (direction.Contains("出港")) return "出港";
        return "综合";
    }

    private AmoebaDirectionMetrics BuildDirectionMetrics(List<DataPoint> points, string direction)
    {
        var dirPoints = points.Where(p => MapDirectionToBucket(p.Direction) == direction).ToList();

        var metrics = new AmoebaDirectionMetrics
        {
            Direction = direction,
            Revenue = dirPoints.Where(p => p.Category == "revenue").Sum(p => p.Amount),
            Cost = dirPoints.Where(p => p.Category == "cost").Sum(p => p.Amount),
            Expense = dirPoints.Where(p => p.Category == "expense").Sum(p => p.Amount),
            AllocatedCost = dirPoints.Where(p => p.Category == "depreciation").Sum(p => p.Amount),
            WaybillCount = dirPoints.Sum(p => p.WaybillCount),
            TotalWeight = dirPoints.Sum(p => p.Weight)
        };

        metrics.Profit = metrics.Revenue - metrics.Cost - metrics.Expense - metrics.AllocatedCost;
        metrics.ProfitRate = metrics.Revenue > 0 ? Math.Round(metrics.Profit / metrics.Revenue * 100, 2) : 0;
        CalculatePerTicketMetrics(metrics);

        return metrics;
    }

    private AmoebaBrandData BuildBrandData(string brandCode, List<DataPoint> points)
    {
        var brandData = new AmoebaBrandData
        {
            BrandCode = brandCode,
            BrandName = points.FirstOrDefault()?.BrandName ?? brandCode
        };

        brandData.Inbound = BuildDirectionMetrics(points, "进港");
        brandData.Outbound = BuildDirectionMetrics(points, "出港");
        brandData.General = BuildDirectionMetrics(points, "综合");

        brandData.BrandRevenue = brandData.Inbound.Revenue + brandData.Outbound.Revenue + brandData.General.Revenue;
        brandData.BrandCost = brandData.Inbound.Cost + brandData.Outbound.Cost + brandData.General.Cost
            + brandData.Inbound.Expense + brandData.Outbound.Expense + brandData.General.Expense
            + brandData.Inbound.AllocatedCost + brandData.Outbound.AllocatedCost + brandData.General.AllocatedCost;
        brandData.BrandProfit = brandData.BrandRevenue - brandData.BrandCost;
        brandData.BrandWaybillCount = brandData.Inbound.WaybillCount + brandData.Outbound.WaybillCount + brandData.General.WaybillCount;
        brandData.BrandTotalWeight = brandData.Inbound.TotalWeight + brandData.Outbound.TotalWeight + brandData.General.TotalWeight;
        brandData.BrandAverageWeight = brandData.BrandWaybillCount > 0 ? brandData.BrandTotalWeight / brandData.BrandWaybillCount : 0;
        brandData.BrandRevenuePerTicket = brandData.BrandWaybillCount > 0 ? brandData.BrandRevenue / brandData.BrandWaybillCount : 0;
        brandData.BrandCostPerTicket = brandData.BrandWaybillCount > 0 ? brandData.BrandCost / brandData.BrandWaybillCount : 0;
        brandData.BrandProfitPerTicket = brandData.BrandWaybillCount > 0 ? brandData.BrandProfit / brandData.BrandWaybillCount : 0;

        return brandData;
    }

    private void CalculatePerTicketMetrics(AmoebaDirectionMetrics metrics)
    {
        if (metrics.WaybillCount > 0)
        {
            metrics.AverageWeight = metrics.TotalWeight / metrics.WaybillCount;
            metrics.RevenuePerTicket = metrics.Revenue / metrics.WaybillCount;
            metrics.CostPerTicket = (metrics.Cost + metrics.Expense + metrics.AllocatedCost) / metrics.WaybillCount;
            metrics.ProfitPerTicket = metrics.Profit / metrics.WaybillCount;
        }
    }

    #endregion

    #region 钻取

    /// <summary>
    /// 获取钻取明细
    /// </summary>
    public async Task<AmoebaDrillDownResponse> GetDrillDownAsync(long unitId, DateTime date, string category, long accountSetId)
    {
        var unit = await _dbContext.Set<FinAuxiliaryItem>().FirstOrDefaultAsync(u => u.FID == unitId && u.FAuxType == "business_unit");
        var orgId = unit?.FOrgId ?? GetCurrentOrgId();
        var response = new AmoebaDrillDownResponse
        {
            UnitName = unit?.FName ?? "",
            Date = date.ToString("yyyy-MM-dd"),
            Category = category
        };

        // 加载映射规则
        var mappingRules = await _dbContext.Set<FinAmoebaMappingRule>()
            .Where(r => r.FOrgId == orgId)
            .OrderByDescending(r => r.FPriority)
            .ToListAsync();

        // 查询该日该单元的凭证分录明细
        var startDate = date.Date;
        var endDate = date.Date.AddDays(1); // 半开区间 [当日0点, 次日0点)，避免 23:59:59 边界漏单(P1-6)

        var entries = await (from e in _voucherEntryRepository.Query()
                            join v in _voucherRepository.Query()
                                // 与主报表一致：草稿凭证不入钻取；排除红冲/作废(P1-5)
                                .Where(v => v.FAccountSetId == accountSetId && v.FStatus >= 1 && !v.FIsRevoked)
                            on e.FVoucherId equals v.FID
                            where v.FDate >= startDate && v.FDate < endDate
                            select new
                            {
                                Entry = e,
                                VoucherSource = v.FSource
                            }).ToListAsync();

        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();
        var accountDict = accounts.ToDictionary(a => a.FID, a => a);

        foreach (var item in entries)
        {
            if (!accountDict.TryGetValue(item.Entry.FAccountId, out var account))
                continue;

            var accCategory = GetAccountCategory(account);
            if (accCategory != category) continue;

            // 与主报表 AggregateVoucherData 同口径：品牌导入/计费生成凭证的收入类分录去重排除
            bool isExcludedSource = !string.IsNullOrEmpty(item.VoucherSource)
                && ExcludedRevenueVoucherSources.Any(s => item.VoucherSource.Contains(s));
            if (isExcludedSource && accCategory == "revenue")
                continue;

            var amount = CalculateAmount(item.Entry, account);
            if (amount == 0) continue;

            // 构造 DataPoint 执行映射，只保留映射到目标 unitId 的分录
            var point = new DataPoint
            {
                Source = "voucher",
                AccountId = item.Entry.FAccountId,
                Summary = item.Entry.FSummary,
                Amount = amount,
                Category = accCategory
            };
            var mappedUnitId = MapToUnit(point, mappingRules);
            if (mappedUnitId != unitId) continue;

            response.Items.Add(new AmoebaDrillDownItem
            {
                Label = account.FName,
                SubLabel = item.Entry.FSummary,
                Amount = amount,
                Source = item.VoucherSource ?? ""
            });
        }

        return response;
    }

    /// <summary>
    /// 损益项明细钻取：按科目分组返回凭证分录列表
    /// </summary>
    public async Task<AmoebaPLItemDetailResponse> GetPLItemDetailAsync(AmoebaPLItemDetailRequest request)
    {
        var accountSetId = request.AccountSetId;

        // 1. 加载指定损益项
        var plItem = await _dbContext.Set<FinAmoebaPLItem>()
            .FirstOrDefaultAsync(i => i.FID == request.PLItemId);
        if (plItem == null)
            return new AmoebaPLItemDetailResponse();

        // 1.1 加载同模板所有项，递归查找所有后代子项
        var allItems = await _dbContext.Set<FinAmoebaPLItem>()
            .Where(i => i.FTemplateId == plItem.FTemplateId)
            .ToListAsync();
        var descendantItems = GetDescendantPLItems(plItem.FID, allItems);
        // 合并自身+所有后代
        var itemsToMatch = new List<FinAmoebaPLItem> { plItem };
        itemsToMatch.AddRange(descendantItems);

        // 2. 与主报表同一管道聚合凭证数据点：自带 FStatus>=1 过滤、品牌导入收入去重、
        //    仅取损益类科目、辅助核算解析——保证下钻明细与报表数字口径一致。
        //    （摘要关键词不参与主报表金额匹配，此处一并废弃，避免下钻多出报表没有的分录）
        var voucherPoints = await AggregateVoucherData(request.StartDate, request.EndDate, GetCurrentOrgId(), accountSetId);

        // 3. 复刻主报表(GetMultiPeriodReportAsync)的两条独立匹配通道与前序独占顺序：每条分录
        //    归属"按前序(文档顺序)首个命中"的叶子项，再筛选归属于当前项（或其后代）的分录。
        //    必须与报表用同一套"项集合 + 顺序"，否则口径漂移：
        //      · 顺序：FSort 是兄弟内序号，跨分区全局排序与前序不一致，前缀重叠科目(5401/540101)判错叶子；
        //      · 集合：指标分区子树在报表里走独立、非互斥于损益项的通道(见 GetMultiPeriodReportAsync
        //        的 indicatorItems 独立匹配)，若与损益项混在一起独占，指标分区叶子会抢走本属损益项的分录。
        var targetIds = itemsToMatch.Select(i => i.FID).ToHashSet();

        var indicatorIds = new HashSet<long>();
        foreach (var sectionRoot in allItems.Where(i => i.F是否指标分区 && i.FParentId == 0))
        {
            foreach (var descendant in allItems.Where(i => IsDescendantOf(i, sectionRoot.FID, allItems)))
                indicatorIds.Add(descendant.FID);
            indicatorIds.Add(sectionRoot.FID);
        }
        // 目标项落在哪条通道(损益 / 指标分区)，就只在该通道的项集合内复刻其独占顺序
        var targetIsIndicator = indicatorIds.Contains(plItem.FID);
        var matchScope = allItems
            .Where(i => indicatorIds.Contains(i.FID) == targetIsIndicator)
            .ToList();
        var preorderIndex = BuildPreorderIndex(matchScope);
        var orderedLeaves = matchScope
            .Where(i => (i.FNodeRole == "data" || i.FNodeRole == "indicator") && !i.FIsManualEntry)
            .OrderBy(i => preorderIndex.GetValueOrDefault(i.FID, int.MaxValue))
            .Select(leaf => new
            {
                Leaf = leaf,
                IsBilling = leaf.FDataSource == "billing" || leaf.F系统数据源 == "billing",
                AcceptableSources = GetAcceptableSourcesForLeaf(leaf),
                Accounts = ParseAccountCodesV2(leaf.FRelatedAccountsJson)
                    .Where(a => !string.IsNullOrEmpty(a.Code))
                    .ToList(),
            })
            .Where(l => !l.IsBilling && l.AcceptableSources.Count > 0 && !AccountLeafIsExcluded(l.Leaf, l.Accounts))
            .ToList();

        var claimedEntryIds = new HashSet<long>();
        foreach (var dp in voucherPoints)
        {
            var claimer = orderedLeaves.FirstOrDefault(l => AccountLeafAccepts(dp, l.AcceptableSources, l.Accounts));
            if (claimer != null && targetIds.Contains(claimer.Leaf.FID) && dp.VoucherEntryId.HasValue)
            {
                claimedEntryIds.Add(dp.VoucherEntryId.Value);
            }
        }

        if (claimedEntryIds.Count == 0)
            return new AmoebaPLItemDetailResponse();

        // 4. 取归属分录的凭证明细
        var entries = await (from e in _voucherEntryRepository.Query()
                            join v in _voucherRepository.Query()
                                .Where(v => v.FAccountSetId == accountSetId && v.FStatus >= 1) // 1=已审核, 2=已过账
                            on e.FVoucherId equals v.FID
                            where claimedEntryIds.Contains(e.FID)
                            select new
                            {
                                Entry = e,
                                VoucherId = v.FID,
                                VoucherWord = v.FVoucherWord,
                                VoucherNo = v.FVoucherNo,
                                VoucherDate = v.FDate
                            }).ToListAsync();

        // 5. 加载科目
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();
        var accountDict = accounts.ToDictionary(a => a.FID, a => a);

        // 6. 组装明细
        var matchedEntries = new List<(long VoucherId, string VoucherWord, int VoucherNo, DateTime VoucherDate, FinVoucherEntry Entry, FinAccount Account)>();

        foreach (var item in entries)
        {
            if (!accountDict.TryGetValue(item.Entry.FAccountId, out var account))
                continue;

            matchedEntries.Add((item.VoucherId, item.VoucherWord, item.VoucherNo, item.VoucherDate, item.Entry, account));
        }

        // 7. 按科目分组
        var grouped = matchedEntries
            .GroupBy(m => m.Account.FID)
            .Select(g =>
            {
                var acc = g.First().Account;
                var entryDetails = g.Select(m =>
                {
                    decimal amount = acc.FBalanceDirection == "贷"
                        ? m.Entry.FCreditAmount - m.Entry.FDebitAmount
                        : m.Entry.FDebitAmount - m.Entry.FCreditAmount;
                    return new VoucherEntryDetail
                    {
                        VoucherId = m.VoucherId,
                        EntryId = m.Entry.FID,
                        VoucherNumber = $"{m.VoucherWord}-{m.VoucherNo:D4}",
                        Date = m.VoucherDate,
                        Summary = m.Entry.FSummary ?? "",
                        DebitAmount = m.Entry.FDebitAmount,
                        CreditAmount = m.Entry.FCreditAmount,
                        Amount = amount
                    };
                }).OrderBy(e => e.Date).ToList();

                return new AccountSummaryItem
                {
                    AccountId = acc.FID,
                    AccountCode = acc.FCode,
                    AccountName = acc.FName,
                    Amount = entryDetails.Sum(e => e.Amount),
                    Entries = entryDetails
                };
            })
            .OrderBy(a => a.AccountCode)
            .ToList();

        return new AmoebaPLItemDetailResponse
        {
            Accounts = grouped,
            TotalAmount = grouped.Sum(a => a.Amount)
        };
    }

    /// <summary>
    /// 出港收入按业务对象类型+名称下钻（从计费结果表聚合）
    /// </summary>
    public async Task<AmoebaBillingDrillDownResponse> GetBillingDrillDownAsync(AmoebaPLItemDetailRequest request)
    {
        var orgId = GetCurrentOrgId();

        // 1. 加载损益项，确认是“出港收入”类型
        var plItem = await _dbContext.Set<FinAmoebaPLItem>()
            .FirstOrDefaultAsync(i => i.FID == request.PLItemId);
        if (plItem == null)
            return new AmoebaBillingDrillDownResponse
            {
                DateRange = $"{request.StartDate:yyyy-MM-dd} ~ {request.EndDate:yyyy-MM-dd}"
            };

        var billingFilter = ParseBillingFilter(plItem.FBillingFilterJson);

        // 2. 确定网点范围（通过经营单元→映射规则→网点）
        List<string>? targetSiteCodes = null;
        if (request.UnitIds != null && request.UnitIds.Count > 0)
        {
            var mappingRules = await _dbContext.Set<FinAmoebaMappingRule>()
                .Where(r => r.FOrgId == orgId)
                .ToListAsync();

            targetSiteCodes = mappingRules
                .Where(r => request.UnitIds.Contains(r.FUnitId) && (r.FDataSourceType == 1 || r.FDataSourceType == 0))
                .Where(r => !string.IsNullOrEmpty(r.FSiteCode))
                .Select(r => r.FSiteCode!)
                .Distinct()
                .ToList();
        }

        if (billingFilter?.Outlets.Count > 0)
        {
            targetSiteCodes = targetSiteCodes == null
                ? billingFilter.Outlets.Distinct().ToList()
                : targetSiteCodes.Intersect(billingFilter.Outlets).ToList();
            if (targetSiteCodes.Count == 0)
            {
                return new AmoebaBillingDrillDownResponse
                {
                    DateRange = $"{request.StartDate:yyyy-MM-dd} ~ {request.EndDate:yyyy-MM-dd}",
                    UnitName = await GetSingleUnitNameAsync(request.UnitIds)
                };
            }
        }

        // 3. 构建 SQL 查询
        var amountExpression = billingFilter?.Aggregation switch
        {
            "waybill_count" => "CAST(COUNT(*) AS decimal(18,2))",
            "weight" => "SUM(ISNULL(br.[F计费重量], 0))",
            _ => "SUM(ISNULL(br.[F应收金额], 0))"
        };
        var sql = @"SELECT 
                    br.[F业务对象类型] AS ClientType,
                    br.[F业务对象编号] AS ClientId,
                    br.[F业务对象名称] AS ClientName,
                    " + amountExpression + @" AS Amount
                FROM [EXP出港运单_计费结果] br
                WHERE br.[F运单日期] >= {0} AND br.[F运单日期] <= {1}
                    AND br.[F组织ID] = {2}";

        var parameters = new List<object> { request.StartDate, request.EndDate, orgId };
        int paramIdx = 3;

        if (billingFilter == null || billingFilter.Scope == "priced")
        {
            sql += " AND br.[F计算状态] = 1";
        }

        if (targetSiteCodes != null && targetSiteCodes.Count > 0)
        {
            var inClause = string.Join(",", targetSiteCodes.Select((_, i) => $"{{{i + paramIdx}}}"));
            sql += $" AND br.[F归属网点编号] IN ({inClause})";
            parameters.AddRange(targetSiteCodes.Cast<object>());
            paramIdx += targetSiteCodes.Count;
        }

        if (billingFilter?.BusinessObjects.Count > 0)
        {
            var inClause = string.Join(",", billingFilter.BusinessObjects.Select((_, i) => $"{{{i + paramIdx}}}"));
            sql += $" AND br.[F业务对象编号] IN ({inClause})";
            parameters.AddRange(billingFilter.BusinessObjects.Cast<object>());
            paramIdx += billingFilter.BusinessObjects.Count;
        }

        sql += @" GROUP BY br.[F业务对象类型], br.[F业务对象编号], br.[F业务对象名称]
                  ORDER BY br.[F业务对象类型], br.[F业务对象编号]";

        var rows = await _dbContext.Database
            .SqlQueryRaw<BillingClientAggRow>(sql, parameters.ToArray())
            .ToListAsync();

        // 3.5 补全业务对象名称（当计费结果表中名称为空时，从CRM客户表查找）
        var clientIdsWithoutName = rows
            .Where(r => string.IsNullOrEmpty(r.ClientName) && !string.IsNullOrEmpty(r.ClientId))
            .Select(r => r.ClientId!)
            .Distinct()
            .ToList();

        if (clientIdsWithoutName.Count > 0)
        {
            var inClauseCrm = string.Join(",", clientIdsWithoutName.Select((_, i) => $"{{{i}}}")); 
            var crmSql = $"SELECT [F编号] AS ClientId, ISNULL([F全称], [F简称]) AS ClientName FROM [CRM客户] WHERE [F编号] IN ({inClauseCrm})";
            var clientNames = await _dbContext.Database
                .SqlQueryRaw<ClientNameRow>(crmSql, clientIdsWithoutName.Cast<object>().ToArray())
                .ToListAsync();

            var nameDict = clientNames.ToDictionary(c => c.ClientId, c => c.ClientName);
            foreach (var r in rows.Where(r => string.IsNullOrEmpty(r.ClientName)))
            {
                if (!string.IsNullOrEmpty(r.ClientId) && nameDict.TryGetValue(r.ClientId, out var name))
                    r.ClientName = name;
            }
        }

        // 4. 构建响应
        var response = new AmoebaBillingDrillDownResponse
        {
            DateRange = $"{request.StartDate:yyyy-MM-dd} ~ {request.EndDate:yyyy-MM-dd}",
            TotalAmount = rows.Sum(r => r.Amount)
        };

        // 尝试获取经营单元名称
        if (request.UnitIds != null && request.UnitIds.Count == 1)
        {
            response.UnitName = await GetSingleUnitNameAsync(request.UnitIds);
        }

        // 5. 按业务对象类型分组 → 业务对象分组（报表周期内汇总）
        var groups = rows
            .GroupBy(r => r.ClientType ?? "")
            .Select(typeGroup => new ClientTypeGroup
            {
                TypeCode = typeGroup.Key,
                TypeName = GetClientTypeName(typeGroup.Key),
                SubTotal = typeGroup.Sum(r => r.Amount),
                Clients = typeGroup
                    .GroupBy(r => r.ClientId ?? "")
                    .Select(clientGroup => new ClientSummary
                    {
                        ClientId = clientGroup.Key,
                        ClientName = clientGroup.First().ClientName ?? "",
                        Amount = clientGroup.Sum(r => r.Amount)
                    })
                    .OrderByDescending(c => c.Amount)
                    .ToList()
            })
            .OrderByDescending(g => g.SubTotal)
            .ToList();

        response.Groups = groups;
        return response;
    }

    private async Task<string> GetSingleUnitNameAsync(List<long>? unitIds)
    {
        if (unitIds == null || unitIds.Count != 1) return string.Empty;
        var unit = await _dbContext.Set<FinAuxiliaryItem>()
            .FirstOrDefaultAsync(u => u.FID == unitIds[0] && u.FAuxType == "business_unit");
        return unit?.FName ?? string.Empty;
    }

    /// <summary>
    /// 判断损益项是否为“出港收入”类型（用于前端路由判断）
    /// </summary>
    public async Task<bool> IsOutboundRevenueItemAsync(long plItemId)
    {
        var item = await _dbContext.Set<FinAmoebaPLItem>()
            .FirstOrDefaultAsync(i => i.FID == plItemId);
        // 新字段优先，否则 fallback 到旧字段
        return item != null &&
               ((item.F值来源 == "system" && item.F系统数据源 == "billing")
                || (item.F值来源 == null && item.FNodeRole == "data" && item.FDataSource == "billing"));
    }

    /// <summary>
    /// 判断损益项是否为"折旧"数据源类型（用于前端路由判断）
    /// </summary>
    public async Task<bool> IsDepreciationItemAsync(long plItemId)
    {
        var item = await _dbContext.Set<FinAmoebaPLItem>()
            .FirstOrDefaultAsync(i => i.FID == plItemId);
        // 新字段优先，否则 fallback 到旧字段
        return item != null &&
               ((item.F值来源 == "system" && item.F系统数据源 == "depreciation")
                || (item.F值来源 == null && item.FDataSource == "depreciation"));
    }

    /// <summary>
    /// 折旧项下钻：返回各资产卡片在指定期间的折旧明细
    /// </summary>
    public async Task<DepreciationDrillDownResponse> GetDepreciationDrillDownAsync(
        long plItemId, DateTime startDate, DateTime endDate, long accountSetId)
    {
        var plItem = await _dbContext.Set<FinAmoebaPLItem>()
            .FirstOrDefaultAsync(i => i.FID == plItemId);
        if (plItem == null)
            return new DepreciationDrillDownResponse();

        var accountConfigs = ParseAccountCodesV2(plItem.FRelatedAccountsJson);
        var orgId = GetCurrentOrgId();
        var query = _dbContext.Set<FinAssetCard>()
            .Where(a => a.FStatus == 1);

        if (accountSetId > 0)
            query = query.Where(a => a.FAccountSetId == accountSetId);
        if (orgId > 0)
            query = query.Where(a => a.FOrgId == orgId);

        var assetCards = await query.ToListAsync();

        var response = new DepreciationDrillDownResponse();
        if (!assetCards.Any())
            return response;

        foreach (var card in assetCards)
        {
            if (card.FUsefulLife == null || card.FUsefulLife <= 0) continue;

            var residualRate = card.FResidualRate ?? 0;
            var monthlyDepreciation = (card.FOriginalValue * (1 - residualRate / 100)) / card.FUsefulLife.Value;
            // 与主报表 AggregateDepreciation 共用同一计提口径（自然月比例 + 启用/提足窗口）
            var periodDepreciation = Math.Round(CalculatePeriodDepreciation(card, startDate, endDate), 2);

            if (periodDepreciation <= 0) continue;

            var accountCode = ExtractDepreciationDebitAccountCode(card.FRemark);
            if (accountConfigs.Count > 0 &&
                (string.IsNullOrEmpty(accountCode) || !accountConfigs.Any(a => !string.IsNullOrEmpty(a.Code) && accountCode.StartsWith(a.Code))))
            {
                continue;
            }

            // 从F备注中提取部门信息
            string? department = null;
            if (!string.IsNullOrEmpty(card.FRemark))
            {
                var match = Regex.Match(card.FRemark, @"部门\w+");
                if (match.Success) department = match.Value;
            }

            response.Assets.Add(new AssetDepreciationDetail
            {
                AssetCardId = card.FID,
                AssetCode = card.FCode,
                AssetName = card.FName,
                OriginalValue = card.FOriginalValue,
                MonthlyDepreciation = Math.Round(monthlyDepreciation, 2),
                PeriodDepreciation = periodDepreciation,
                Department = department
            });
        }

        response.TotalAmount = response.Assets.Sum(a => a.PeriodDepreciation);
        return response;
    }

    private static string? ExtractDepreciationDebitAccountCode(string? remark)
    {
        if (string.IsNullOrWhiteSpace(remark)) return null;
        var match = Regex.Match(remark, @"借:(\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// 业务对象类型编码转中文名称
    /// </summary>
    private static string GetClientTypeName(string typeCode)
    {
        return typeCode switch
        {
            "KH" => "直接客户",
            "DL" => "代理商",
            "WD" => "快递网点",
            "YW" => "业务员",
            "CB" => "承包区",
            "YZ" => "末端驿站",
            _ => "其他"
        };
    }

    #endregion

    #region 手工分类

    /// <summary>
    /// 获取未分类凭证分录
    /// </summary>
    public async Task<List<AmoebaUnclassifiedDto>> GetUnclassifiedAsync(DateTime startDate, DateTime endDate, long orgId, long accountSetId)
    {
        // 已分类的分录ID
        var classifiedEntryIds = await _dbContext.Set<FinAmoebaManualClassify>()
            .Where(m => m.FOrgId == orgId)
            .Select(m => m.FVoucherEntryId)
            .ToListAsync();

        var entries = await (from e in _voucherEntryRepository.Query()
                            join v in _voucherRepository.Query()
                                .Where(v => v.FAccountSetId == accountSetId)
                            on e.FVoucherId equals v.FID
                            // 半开区间，避免月末当天带时分的分录漏入"未分类"判断(P1-6)
                            where v.FDate >= startDate && v.FDate < endDate.AddDays(1)
                                && !classifiedEntryIds.Contains(e.FID)
                            select new
                            {
                                Entry = e,
                                VoucherDate = v.FDate,
                                VoucherSource = v.FSource
                            }).ToListAsync();

        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();
        var accountDict = accounts.ToDictionary(a => a.FID, a => a);

        // 加载损益项进行匹配测试——仅限本账套下模板的损益项：
        // 用全库模板判定会把"别的模板配了该科目"误判为可自动分类，漏报未分类分录
        var templateIds = await _dbContext.Set<FinAmoebaPLTemplate>()
            .Where(t => accountSetId <= 0 || t.FAccountSetId == accountSetId)
            .Select(t => t.FID)
            .ToListAsync();
        var plItems = await _dbContext.Set<FinAmoebaPLItem>()
            .Where(i => templateIds.Contains(i.FTemplateId))
            .ToListAsync();

        var result = new List<AmoebaUnclassifiedDto>();

        foreach (var item in entries)
        {
            if (!accountDict.TryGetValue(item.Entry.FAccountId, out var account))
                continue;

            var category = GetAccountCategory(account);
            if (string.IsNullOrEmpty(category)) continue;

            // 测试能否被自动分类（科目匹配+关键词匹配）
            bool canAutoClassify = false;
            if (plItems.Any(pi => !string.IsNullOrEmpty(pi.FRelatedAccountsJson)))
            {
                foreach (var pi in plItems.Where(i => !string.IsNullOrEmpty(i.FRelatedAccountsJson)))
                {
                    var accountCodes = ParseAccountCodePrefixes(pi.FRelatedAccountsJson);
                    if (accountCodes.Any(code => account.FCode.StartsWith(code)) && pi.FNodeRole == "data")
                    {
                        canAutoClassify = true;
                        break;
                    }
                }
            }

            if (!canAutoClassify && !string.IsNullOrEmpty(item.Entry.FSummary))
            {
                foreach (var pi in plItems.Where(i => !string.IsNullOrEmpty(i.FSummaryKeywordsJson)))
                {
                    try
                    {
                        var keywords = JsonSerializer.Deserialize<List<string>>(pi.FSummaryKeywordsJson!);
                        if (keywords != null && keywords.Any(kw => item.Entry.FSummary.Contains(kw, StringComparison.OrdinalIgnoreCase)) && pi.FNodeRole == "data")
                        {
                            canAutoClassify = true;
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (canAutoClassify) continue;

            var amount = CalculateAmount(item.Entry, account);

            // 提取品牌和网点
            string? brandName = null;
            string? siteCode = null;
            if (!string.IsNullOrEmpty(item.Entry.FAuxiliaryJson))
            {
                var auxItems = ParseAuxiliaryJson(item.Entry.FAuxiliaryJson);
                foreach (var aux in auxItems)
                {
                    if (aux.Type == "express_brand") brandName = aux.Name;
                    if (aux.Type == "outlet") siteCode = aux.Code;
                }
            }

            result.Add(new AmoebaUnclassifiedDto
            {
                EntryId = item.Entry.FID,
                Date = item.VoucherDate,
                AccountName = account.FName,
                Summary = item.Entry.FSummary,
                Amount = amount,
                BrandName = brandName,
                SiteCode = siteCode
            });
        }

        return result;
    }

    /// <summary>
    /// 批量标记分类
    /// </summary>
    public async Task BatchClassifyAsync(AmoebaBatchClassifyRequest request, long orgId)
    {
        var now = DateTime.Now;
        foreach (var item in request.Items)
        {
            var existing = await _dbContext.Set<FinAmoebaManualClassify>()
                .FirstOrDefaultAsync(m => m.FVoucherEntryId == item.EntryId && m.FOrgId == orgId);

            if (existing != null)
            {
                existing.FPLItemId = item.PLItemId;
                existing.FMarkedTime = now;
            }
            else
            {
                _dbContext.Set<FinAmoebaManualClassify>().Add(new FinAmoebaManualClassify
                {
                    FVoucherEntryId = item.EntryId,
                    FPLItemId = item.PLItemId,
                    FMarkedTime = now,
                    FOrgId = orgId
                });
            }
        }

        await _dbContext.SaveChangesAsync();
    }

    #endregion

    #region 映射规则 CRUD

    public async Task<List<AmoebaMappingRuleDto>> GetMappingRulesAsync(long orgId)
    {
        var units = await _dbContext.Set<FinAuxiliaryItem>()
            .Where(u => u.FOrgId == orgId && u.FAuxType == "business_unit")
            .ToDictionaryAsync(u => u.FID, u => u.FName);

        var rules = await _dbContext.Set<FinAmoebaMappingRule>()
            .Where(r => r.FOrgId == orgId)
            .OrderByDescending(r => r.FPriority)
            .ToListAsync();

        return rules.Select(r => new AmoebaMappingRuleDto
        {
            Id = r.FID,
            UnitId = r.FUnitId,
            UnitName = units.GetValueOrDefault(r.FUnitId),
            DataSourceType = r.FDataSourceType,
            SiteCode = r.FSiteCode,
            BrandCode = r.FBrandCode,
            Direction = r.FDirection,
            AuxField = r.FAuxField,
            AuxValue = r.FAuxValue,
            Priority = r.FPriority,
            Remark = r.FRemark
        }).ToList();
    }

    public async Task<AmoebaMappingRuleDto> CreateMappingRuleAsync(CreateMappingRuleRequest request, long orgId)
    {
        var entity = new FinAmoebaMappingRule
        {
            FUnitId = request.UnitId,
            FDataSourceType = request.DataSourceType,
            FSiteCode = request.SiteCode,
            FBrandCode = request.BrandCode,
            FDirection = request.Direction,
            FAuxField = request.AuxField,
            FAuxValue = request.AuxValue,
            FPriority = request.Priority,
            FRemark = request.Remark,
            FOrgId = orgId
        };

        _dbContext.Set<FinAmoebaMappingRule>().Add(entity);
        await _dbContext.SaveChangesAsync();

        return new AmoebaMappingRuleDto
        {
            Id = entity.FID,
            UnitId = entity.FUnitId,
            DataSourceType = entity.FDataSourceType,
            SiteCode = entity.FSiteCode,
            BrandCode = entity.FBrandCode,
            Direction = entity.FDirection,
            AuxField = entity.FAuxField,
            AuxValue = entity.FAuxValue,
            Priority = entity.FPriority,
            Remark = entity.FRemark
        };
    }

    public async Task<AmoebaMappingRuleDto?> UpdateMappingRuleAsync(long id, CreateMappingRuleRequest request, long orgId)
    {
        var entity = await _dbContext.Set<FinAmoebaMappingRule>()
            .FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);

        if (entity == null) return null;

        entity.FUnitId = request.UnitId;
        entity.FDataSourceType = request.DataSourceType;
        entity.FSiteCode = request.SiteCode;
        entity.FBrandCode = request.BrandCode;
        entity.FDirection = request.Direction;
        entity.FAuxField = request.AuxField;
        entity.FAuxValue = request.AuxValue;
        entity.FPriority = request.Priority;
        entity.FRemark = request.Remark;

        await _dbContext.SaveChangesAsync();

        var unitName = await _dbContext.Set<FinAuxiliaryItem>()
            .Where(u => u.FID == entity.FUnitId && u.FAuxType == "business_unit")
            .Select(u => u.FName)
            .FirstOrDefaultAsync();

        return new AmoebaMappingRuleDto
        {
            Id = entity.FID,
            UnitId = entity.FUnitId,
            UnitName = unitName,
            DataSourceType = entity.FDataSourceType,
            SiteCode = entity.FSiteCode,
            BrandCode = entity.FBrandCode,
            Direction = entity.FDirection,
            AuxField = entity.FAuxField,
            AuxValue = entity.FAuxValue,
            Priority = entity.FPriority,
            Remark = entity.FRemark
        };
    }

    public async Task<bool> DeleteMappingRuleAsync(long id, long orgId)
    {
        var entity = await _dbContext.Set<FinAmoebaMappingRule>()
            .FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);

        if (entity == null) return false;

        _dbContext.Set<FinAmoebaMappingRule>().Remove(entity);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    #endregion


    #region 辅助方法

    /// <summary>
    /// 获取科目的损益分类
    /// </summary>
    private static string GetAccountCategory(FinAccount account)
    {
        var code = account.FCode;
        // 损益类科目范围: 5xxx
        if (!code.StartsWith("5")) return "";

        // 收入类: 编码50xx-53xx范围
        if (code.StartsWith("50") || code.StartsWith("51") || code.StartsWith("52") || code.StartsWith("53"))
            return "revenue";

        // 成本类: 编码54xx-55xx范围（主营业务成本/其他业务成本）
        if (code.StartsWith("54") || code.StartsWith("55"))
            return "cost";

        // 费用类: 编码56xx-57xx范围（期间费用）
        if (code.StartsWith("56") || code.StartsWith("57"))
            return "expense";

        return "";
    }

    private record AuxItem(string Type, long Id, string Code, string Name);

    private static List<AuxItem> ParseAuxiliaryJson(string json)
    {
        var result = new List<AuxItem>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in doc.RootElement.EnumerateArray())
                    result.Add(ParseAuxElement(elem));
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                result.Add(ParseAuxElement(doc.RootElement));
            }
        }
        catch { }
        return result;
    }

    private static AuxItem ParseAuxElement(JsonElement elem)
    {
        var type = elem.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
        long id = 0;
        if (elem.TryGetProperty("id", out var idProp))
        {
            if (idProp.ValueKind == JsonValueKind.Number)
                id = idProp.GetInt64();
            else if (idProp.ValueKind == JsonValueKind.String && long.TryParse(idProp.GetString(), out var parsed))
                id = parsed;
        }
        var code = elem.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
        var name = elem.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        return new AuxItem(type, id, code, name);
    }

    /// <summary>
    /// 计算单条分录金额
    /// </summary>
    private static decimal CalculateAmount(FinVoucherEntry entry, FinAccount account)
    {
        // 收入类科目：贷方 - 借方
        if (account.FBalanceDirection == "贷")
        {
            return entry.FCreditAmount - entry.FDebitAmount;
        }

        // 成本/费用类科目：借方 - 贷方
        return entry.FDebitAmount - entry.FCreditAmount;
    }

    /// <summary>
    /// 从FSource提取品牌编码
    /// </summary>
    private static string? ExtractBrandCode(string? source)
    {
        if (string.IsNullOrEmpty(source)) return null;

        // "极兔导入" → "JT", "韵达导入" → "YD", "申通导入" → "ST"
        if (source.Contains("极兔")) return "JT";
        if (source.Contains("韵达")) return "YD";
        if (source.Contains("申通")) return "ST";

        return null;
    }

    #endregion

    #region Coverage Check

    /// <summary>
    /// 模板科目覆盖率诊断：复用报表匹配逻辑，返回未匹配的科目明细
    /// </summary>
    public async Task<AmoebaCoverageReportDto> GetCoverageReportAsync(long templateId, string period, long accountSetId, long orgId)
    {
        if (orgId <= 0) orgId = GetCurrentOrgId();

        // 1. 加载模板损益项
        var plItems = await _dbContext.Set<FinAmoebaPLItem>()
            .Where(i => i.FTemplateId == templateId)
            .OrderBy(i => i.FSort)
            .ToListAsync();

        // 2. 计算期间日期范围
        var (startDate, endDate) = PeriodToDateRange(period);

        // 3. 获取当期凭证 DataPoint（复用已有逻辑）
        var voucherData = await AggregateVoucherData(startDate, endDate, orgId, accountSetId);
        var billingData = await AggregateBillingRevenue(startDate, endDate, orgId, plItems, new List<FinAmoebaMappingRule>(), null);
        var depreciationData = await AggregateDepreciation(startDate, endDate, orgId, accountSetId);
        var allDataPoints = new List<DataPoint>();
        allDataPoints.AddRange(voucherData);
        allDataPoints.AddRange(billingData);
        allDataPoints.AddRange(depreciationData);

        // 4. 执行独占匹配
        var (matched, unmatched) = MatchDataPointsToPLItems(allDataPoints, plItems);

        // 5. 收集模板已配置的科目编码前缀（兼容新旧两种格式，仅取数据项且非手工填报）
        var configuredCodes = new List<string>();
        foreach (var item in plItems.Where(i => i.FNodeRole == "data" && !i.FIsManualEntry))
        {
            if (!string.IsNullOrEmpty(item.FRelatedAccountsJson))
            {
                try
                {
                    var accountsWithFilter = ParseAccountCodesV2(item.FRelatedAccountsJson);
                    foreach (var acc in accountsWithFilter.Where(a => !string.IsNullOrEmpty(a.Code)))
                    {
                        configuredCodes.Add(acc.Code);
                    }
                }
                catch { }
            }
        }
        configuredCodes = configuredCodes.Distinct().OrderBy(c => c).ToList();

        // 6. 按科目编码聚合未匹配 DataPoint（含 voucher 与 depreciation 两种带科目编码的数据源）
        var uncoveredAccounts = unmatched
            .Where(dp => (dp.Source == "voucher" || dp.Source == "depreciation") && !string.IsNullOrEmpty(dp.AccountCode))
            .GroupBy(dp => dp.AccountCode!)
            .Select(g =>
            {
                // 查找科目名称（从 allDataPoints 中的 accountDict 或直接查库）
                var first = g.First();
                var category = first.Category;
                string? suggestedDir = category == "revenue" ? "出港"
                    : category == "cost" ? "出港"
                    : category == "expense" ? "综合"
                    : null;
                return new AmoebaUncoveredAccountDto
                {
                    AccountCode = g.Key,
                    AccountName = "", // 下面补充
                    AccountCategory = category,
                    EntryCount = g.Count(),
                    TotalAmount = g.Sum(dp => dp.Amount),
                    SuggestedTab = suggestedDir
                };
            })
            .OrderByDescending(a => a.TotalAmount)
            .ToList();

        // 7. 补充科目名称
        var uncoveredCodes = uncoveredAccounts.Select(a => a.AccountCode).ToList();
        if (uncoveredCodes.Count > 0)
        {
            var accounts = await _accountRepository.Query()
                .Where(a => a.FAccountSetId == accountSetId)
                .ToListAsync();
            var accountByCode = accounts.ToDictionary(a => a.FCode, a => a);
            foreach (var ua in uncoveredAccounts)
            {
                if (accountByCode.TryGetValue(ua.AccountCode, out var acct))
                    ua.AccountName = acct.FName;
                else
                    ua.AccountName = "(科目不存在)";
            }
        }

        // 8. 构建报告
        // 仅计入涉及科目的数据源（voucher + depreciation）用于覆盖率计算，billing 数据源没有科目编码，应排除
        var accountBasedDataPoints = allDataPoints.Where(dp => dp.Source == "voucher" || dp.Source == "depreciation").ToList();
        var accountBasedUnmatched = unmatched.Where(dp => (dp.Source == "voucher" || dp.Source == "depreciation") && !string.IsNullOrEmpty(dp.AccountCode)).ToList();
        int total = accountBasedDataPoints.Count;
        decimal unmatchedAmount = accountBasedUnmatched.Sum(dp => dp.Amount);
        decimal coverageRate = total > 0 ? Math.Round((decimal)(total - accountBasedUnmatched.Count) / total * 100, 1) : 100;

        return new AmoebaCoverageReportDto
        {
            TemplateId = templateId,
            Period = period,
            TotalDataPoints = total,
            MatchedDataPoints = total - accountBasedUnmatched.Count,
            UnmatchedDataPoints = accountBasedUnmatched.Count,
            UnmatchedAmount = unmatchedAmount,
            CoverageRate = coverageRate,
            ConfiguredAccountCodes = configuredCodes,
            UncoveredAccounts = uncoveredAccounts
        };
    }

    #endregion

    #region 内部模型

    private class BillingAggRow
    {
        public string? SiteCode { get; set; }
        public string? BrandCode { get; set; }
        public string? BusinessObjectCode { get; set; }
        public int CalcStatus { get; set; }
        public decimal TotalAmount { get; set; }
        public int WaybillCount { get; set; }
        public decimal TotalWeight { get; set; }
    }

    private class BillingClientAggRow
    {
        public string? ClientType { get; set; }
        public string? ClientId { get; set; }
        public string? ClientName { get; set; }
        public decimal Amount { get; set; }
    }

    private class ClientNameRow
    {
        public string ClientId { get; set; } = "";
        public string ClientName { get; set; } = "";
    }

    #endregion
}

/// <summary>
/// 内部数据点
/// </summary>
internal class DataPoint
{
    public string Source { get; set; } = "";         // "billing" / "voucher" / "depreciation"
    public string? SiteCode { get; set; }
    public string? BrandCode { get; set; }
    public string? BrandName { get; set; }
    public string? Direction { get; set; }           // 进港/出港/综合/null(待分类)
    public long? AccountId { get; set; }
    public string? AccountCode { get; set; }
    public string? Summary { get; set; }             // 凭证摘要
    public decimal Amount { get; set; }
    public string Category { get; set; } = "";       // revenue/cost/expense/depreciation
    public int WaybillCount { get; set; }
    public decimal Weight { get; set; }
    public long? VoucherEntryId { get; set; }        // 凭证分录ID（用于手工分类）
    public bool IsPriced { get; set; }                // billing数据源：是否已计价（F计算状态=1）
    public long? BusinessUnitId { get; set; }        // 辅助核算中的经营单元ID
    public long? MappedUnitId { get; set; }          // 映射后的经营单元ID
    public long? PLItemId { get; set; }              // 匹配到的损益项ID
    /// <summary>所有辅助核算类型→编码 的映射，供损益项级辅助核算过滤使用</summary>
    public Dictionary<string, string>? AuxValues { get; set; }
}
