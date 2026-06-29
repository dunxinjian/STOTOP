using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// 报价快照（扁平化缓存结构）
/// </summary>
public class QuotationSnapshot
{
    public long QuotationId { get; set; }
    public string? PlanCode { get; set; }
    public string? ClientType { get; set; }       // KH/DL/WD/YW/CB/YZ
    public string? ClientId { get; set; }          // 自然编号
    public string? NetworkPointCode { get; set; }
    public long? NetworkPointId { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public int SettlementWeightStage { get; set; }
    public int ThrowRatio { get; set; }
    public decimal? InsuranceRate { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    // 共享店铺
    public bool SharedShopEnabled { get; set; }
    public HashSet<string> Aliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // 佣金配置（内嵌）
    public bool HasCommission { get; set; }
    public int CommissionCalcMethod { get; set; }
    public decimal? CommissionRate { get; set; }
    public decimal? CommissionFixedAmount { get; set; }
    public decimal? CommissionWeightAmount { get; set; }
    public string? CommissionTargetType { get; set; }
    public string? CommissionTargetId { get; set; }
    public string? ClientName { get; set; }
    public string? CommissionTargetName { get; set; }
}

/// <summary>
/// 报价查找缓存 — 替代 ShopAssignmentCache + ClientHierarchyCache
/// 核心索引: ShopName → List&lt;QuotationSnapshot&gt;
/// </summary>
public class QuotationLookupCache
{
    /// <summary>ShopName → 该店铺关联的所有报价快照</summary>
    private readonly Dictionary<string, List<QuotationSnapshot>> _shopIndex
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>诊断：已加载的生效报价数量</summary>
    public int DiagQuotationCount { get; private set; }
    /// <summary>诊断：已加载的店铺关联数量</summary>
    public int DiagShopLinkCount { get; private set; }
    /// <summary>诊断：索引中唯一店铺数量</summary>
    public int DiagUniqueShopCount { get; private set; }

    /// <summary>QuotationId → QuotationSnapshot</summary>
    private readonly Dictionary<long, QuotationSnapshot> _idIndex = new();

    /// <summary>网点编号(FCode) → 组织ID(FOrgId)</summary>
    private readonly Dictionary<string, long> _networkPointMap
        = new(StringComparer.OrdinalIgnoreCase);

    public async Task LoadAsync(
        IRepository<ExpQuotation> quotationRepo,
        IRepository<ExpQuotationShop> quotationShopRepo,
        IRepository<ExpNetworkPoint> networkPointRepo,
        IRepository<ExpQuotationCommission> commissionRepo,
        IRepository<ExpQuotationAlias> aliasRepo,
        IRepository<ExpAgent> agentRepo,
        IRepository<ExpSalesman> salesmanRepo,
        IRepository<ExpFranchiseArea> franchiseAreaRepo,
        IRepository<ExpLastMileStation> stationRepo,
        long orgId)
    {
        // 1. 加载所有生效报价: FStatus = 1（显式按组织过滤，纵深防御：后台链路即使组织过滤器失效也不串组织）
        var quotations = await quotationRepo.Query()
            .Where(q => q.FStatus == 1 && q.FOrgId == orgId)
            .ToListAsync();

        // 2. 加载关联店铺
        var quotationShops = await quotationShopRepo.Query().ToListAsync();

        // 3. 加载网点，构建编号→ID映射（编号在关联的 SysOrganization.FCode）
        var networkPoints = await networkPointRepo.Query()
            .Include(np => np.Organization)
            .ToListAsync();
        foreach (var np in networkPoints)
        {
            var code = np.Organization?.FCode;
            if (!string.IsNullOrEmpty(code))
                _networkPointMap.TryAdd(code, np.FOrgId);
        }

        // 4. 加载佣金配置 (FEnabled = true)。仅取本次已加载报价的佣金（佣金表无组织字段，靠报价集合间接隔离）。
        // 同一报价存在多条启用佣金时取最新一条——ToDictionary 遇重复键会抛异常，
        // 单个报价的配置问题不能拖垮整批计费
        var quotationIds = quotations.Select(q => q.FID).ToList();
        var commissions = await commissionRepo.Query()
            .Where(c => c.FEnabled && quotationIds.Contains(c.FQuotationId))
            .ToListAsync();
        var commissionMap = commissions
            .GroupBy(c => c.FQuotationId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.FID).First());

        // 5. 加载共享别名，按报价ID分组
        var aliases = await aliasRepo.Query().ToListAsync();
        var aliasMap = aliases.GroupBy(a => a.FQuotationId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => a.FAlias).ToHashSet(StringComparer.OrdinalIgnoreCase));

        // 6. 构建统一的 编号→名称 字典（所有业务对象类型合并）
        var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // DL: 代理
        var agents = await agentRepo.Query()
            .Where(a => a.FCode != null && a.FCode != "")
            .Select(a => new { a.FCode, a.FName }).ToListAsync();
        foreach (var a in agents) nameMap.TryAdd(a.FCode!, a.FName);

        // WD: 网点（使用简称）
        var points = await networkPointRepo.Query()
            .Where(np => np.FCode != null && np.FCode != "")
            .Select(np => new { np.FCode, np.FShortName }).ToListAsync();
        foreach (var np in points)
            if (!string.IsNullOrEmpty(np.FShortName)) nameMap.TryAdd(np.FCode!, np.FShortName);

        // YW: 业务员（主键是 FEmployeeNo）
        var salesmen = await salesmanRepo.Query()
            .Select(s => new { s.FEmployeeNo, s.FName }).ToListAsync();
        foreach (var s in salesmen) nameMap.TryAdd(s.FEmployeeNo, s.FName);

        // CB: 承包区
        var areas = await franchiseAreaRepo.Query()
            .Where(fa => fa.FCode != null && fa.FCode != "")
            .Select(fa => new { fa.FCode, fa.FContractor }).ToListAsync();
        foreach (var fa in areas)
            if (!string.IsNullOrEmpty(fa.FContractor)) nameMap.TryAdd(fa.FCode!, fa.FContractor);

        // YZ: 驿站
        var stations = await stationRepo.Query()
            .Where(st => st.FCode != null && st.FCode != "")
            .Select(st => new { st.FCode, st.FName }).ToListAsync();
        foreach (var st in stations)
            if (!string.IsNullOrEmpty(st.FName)) nameMap.TryAdd(st.FCode!, st.FName);

        // 7. 构建快照和索引
        var snapshotMap = new Dictionary<long, QuotationSnapshot>();
        foreach (var q in quotations)
        {
            var snapshot = new QuotationSnapshot
            {
                QuotationId = q.FID,
                PlanCode = q.FPlanCode,
                ClientType = q.FClientType,
                ClientId = q.FBusinessObjectId,
                NetworkPointCode = q.FNetworkPointCode,
                BrandCode = q.FBrandCode,
                SettlementWeightStage = q.FSettlementWeightStage,
                ThrowRatio = q.FThrowRatio,
                InsuranceRate = q.FInsuranceRate,
                EffectiveDate = q.FEffectiveDate,
                SharedShopEnabled = q.FSharedShopEnabled
            };

            // 解析网点ID
            if (!string.IsNullOrEmpty(snapshot.NetworkPointCode) &&
                _networkPointMap.TryGetValue(snapshot.NetworkPointCode, out var npId))
            {
                snapshot.NetworkPointId = npId;
            }

            // 填充别名
            if (aliasMap.TryGetValue(q.FID, out var aliasSet))
                snapshot.Aliases = aliasSet;

            // 填充客户名称
            if (!string.IsNullOrEmpty(snapshot.ClientId) &&
                nameMap.TryGetValue(snapshot.ClientId, out var clientName))
            {
                snapshot.ClientName = clientName;
            }

            // 填充佣金
            if (commissionMap.TryGetValue(q.FID, out var commission))
            {
                snapshot.HasCommission = true;
                snapshot.CommissionCalcMethod = commission.FCalcMethod;
                snapshot.CommissionRate = commission.FRate;
                snapshot.CommissionFixedAmount = commission.FFixedAmount;
                snapshot.CommissionWeightAmount = commission.FWeightAmount;
                snapshot.CommissionTargetType = commission.FTargetClientType;
                snapshot.CommissionTargetId = commission.FTargetClientId;

                // 填充佣金目标名称
                if (!string.IsNullOrEmpty(commission.FTargetClientId) &&
                    nameMap.TryGetValue(commission.FTargetClientId, out var targetName))
                {
                    snapshot.CommissionTargetName = targetName;
                }
            }

            snapshotMap[q.FID] = snapshot;
            _idIndex[q.FID] = snapshot;
        }

        // 按店铺分组建索引
        var shopGroups = quotationShops.GroupBy(s => s.FShopName);
        foreach (var group in shopGroups)
        {
            var shopName = group.Key;
            if (string.IsNullOrEmpty(shopName)) continue;

            var list = new List<QuotationSnapshot>();
            foreach (var shopLink in group)
            {
                if (snapshotMap.TryGetValue(shopLink.FQuotationId, out var snap))
                    list.Add(snap);
            }
            if (list.Count > 0)
                _shopIndex[shopName] = list;
        }

        // 按 EffectiveDate DESC 排序，确保遍历时首个命中即最新生效报价；
        // 同日生效时按 QuotationId DESC（后创建优先）保证命中结果确定
        foreach (var sortList in _shopIndex.Values)
            sortList.Sort((a, b) =>
            {
                var cmp = Nullable.Compare(b.EffectiveDate, a.EffectiveDate);
                return cmp != 0 ? cmp : b.QuotationId.CompareTo(a.QuotationId);
            });

        // 诊断统计赋值
        DiagQuotationCount = quotations.Count;
        DiagShopLinkCount = quotationShops.Count;
        DiagUniqueShopCount = _shopIndex.Count;
    }

    /// <summary>
    /// 核心查找：按店铺+品牌+类型+日期+别名 → 唯一报价
    /// 共享报价通过别名自动校验
    /// </summary>
    public QuotationSnapshot? FindQuotation(string shopName, string brandCode, string clientType, DateTime date, string? alias = null)
    {
        if (!_shopIndex.TryGetValue(shopName, out var list)) return null;

        foreach (var q in list)
        {
            if (q.BrandCode != brandCode || q.ClientType != clientType) continue;
            if (q.EffectiveDate.HasValue && q.EffectiveDate > DateOnly.FromDateTime(date)) continue;

            // WD(网点)类型不使用共享店铺逻辑：业务规则要求网点报价不受共享开关影响，
            // 即使报价误启用了共享开关也直接跳过别名校验，避免运单无别名时无法匹配到WD报价
            if (q.SharedShopEnabled && clientType != "WD")
            {
                // 共享报价：仅当别名列表非空时才执行别名校验
                // 若别名列表为空（未配置别名），则视为普通报价直接匹配
                if (q.Aliases.Count > 0)
                {
                    if (alias == null || !q.Aliases.Contains(alias)) continue;
                }
            }

            return q;
        }
        return null;
    }

    /// <summary>按ID查找</summary>
    public QuotationSnapshot? FindById(long quotationId) => _idIndex.GetValueOrDefault(quotationId);

    /// <summary>网点编号→ID映射</summary>
    public long? ResolveNetworkPointId(string? code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        return _networkPointMap.TryGetValue(code, out var id) ? id : null;
    }
}
