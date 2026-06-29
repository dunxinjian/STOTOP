using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Express.Models;
using System.Data;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 报价方案服务
/// </summary>
public class QuotationService : IQuotationService
{
    private readonly IRepository<ExpQuotation> _planRepo;
    private readonly IRepository<ExpBrand> _brandRepo;
    private readonly IRepository<ExpProvince> _provinceRepo;
    private readonly STOTOPDbContext _dbContext;
    private readonly IOrgContextAccessor _orgContextAccessor;

    public QuotationService(
        IRepository<ExpQuotation> planRepo,
        IRepository<ExpBrand> brandRepo,
        IRepository<ExpProvince> provinceRepo,
        STOTOPDbContext dbContext,
        IOrgContextAccessor orgContextAccessor)
    {
        _planRepo = planRepo;
        _brandRepo = brandRepo;
        _provinceRepo = provinceRepo;
        _dbContext = dbContext;
        _orgContextAccessor = orgContextAccessor;
    }

    public async Task<PagedResult<QuotationListItemDto>> GetListAsync(QuotationQueryRequest request)
    {
        var query = _planRepo.Query();

        if (!string.IsNullOrWhiteSpace(request.BrandCode))
            query = query.Where(e => e.FBrandCode == request.BrandCode);
        if (request.Status.HasValue)
            query = query.Where(e => e.FStatus == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(e => e.FPlanName.Contains(keyword));
        }
        if (!string.IsNullOrWhiteSpace(request.ClientType))
            query = query.Where(e => e.FClientType == request.ClientType);
        if (!string.IsNullOrWhiteSpace(request.ClientId))
            query = query.Where(e => e.FBusinessObjectId == request.ClientId);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.FEffectiveDate)
            .ThenByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        // 批量查询品牌名称
        var brandCodes = items.Select(e => e.FBrandCode).Distinct().ToList();
        var brandNames = await _brandRepo.Query()
            .Where(b => brandCodes.Contains(b.FCode))
            .ToDictionaryAsync(b => b.FCode, b => b.FName);

        // 批量查询报价关联的店铺数量
        var planIds = items.Select(e => e.FID).ToList();
        var shopCounts = await _dbContext.Set<ExpQuotationShop>()
            .Where(s => planIds.Contains(s.FQuotationId))
            .GroupBy(s => s.FQuotationId)
            .Select(g => new { QuotationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.QuotationId, x => x.Count);

        return new PagedResult<QuotationListItemDto>
        {
            Items = items.Select(e => MapToListItemDto(e, brandNames, shopCounts)).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<QuotationDto?> GetByIdAsync(long id)
    {
        // 详情页按当前组织隔离访问（不再绕过组织过滤器，防止跨组织越权读取）
        var entity = await _planRepo.Query()
            .FirstOrDefaultAsync(e => e.FID == id);

        if (entity == null) return null;

        var brandName = await _brandRepo.Query()
            .Where(b => b.FCode == entity.FBrandCode)
            .Select(b => b.FName)
            .FirstOrDefaultAsync();

        return MapToDto(entity, brandName);
    }

    public async Task<QuotationDto> CreateAsync(CreateQuotationRequest request)
    {
        NormalizeSegmentsAndCells(request.Segments);
        ValidateSegments(request.Segments);
        await ValidateMatrixCompleteness(request.Segments, request.AllowIncomplete);

        var matrix = BuildMatrixFromRequest(request.Segments);

        var entity = new ExpQuotation
        {
            FBrandCode = request.BrandCode,
            FPlanName = request.PlanName,
            FPlanCode = request.PlanCode,
            FClientType = request.ClientType,
            FBusinessObjectId = request.ClientId,
            FNetworkPointCode = request.NetworkPointCode,
            FSharedShopEnabled = request.SharedShopEnabled,
            F重量进位方式 = request.WeightRoundingMethod,
            FSettlementWeightStage = request.SettlementWeightStage,
            FEffectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.Today),
            FStatus = 0,
            FPaymentMode = request.PaymentMode,
            FPrepayRatio = request.PrepayRatio,
            FBillingCycle = request.BillingCycle,
            FBillingDay = request.BillingDay,
            FPaymentDueDay = request.PaymentDueDay,
            FThrowRatio = request.ThrowRatio,
            FInsuranceRate = request.InsuranceRate,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now,
            FMatrixJson = PricingMatrixSerializer.Serialize(matrix)
        };

        var result = await _planRepo.AddAsync(entity);

        return (await GetByIdAsync(result.FID))!;
    }

    public async Task<QuotationDto?> UpdateAsync(long id, UpdateQuotationRequest request)
    {
        var entity = await _planRepo.Query()
            .FirstOrDefaultAsync(e => e.FID == id);

        if (entity == null) return null;

        NormalizeSegmentsAndCells(request.Segments);
        ValidateSegments(request.Segments);
        await ValidateMatrixCompleteness(request.Segments, request.AllowIncomplete);

        var matrix = BuildMatrixFromRequest(request.Segments);

        // 更新方案基本信息
        entity.FPlanName = request.PlanName;
        // 编辑页不回传方案编号；null 视为"不修改"，避免每次保存把已有编号清空
        entity.FPlanCode = request.PlanCode ?? entity.FPlanCode;
        entity.FClientType = request.ClientType;
        entity.FBusinessObjectId = request.ClientId;
        entity.FNetworkPointCode = request.NetworkPointCode;
        entity.FSharedShopEnabled = request.SharedShopEnabled;
        entity.F重量进位方式 = request.WeightRoundingMethod;
        entity.FSettlementWeightStage = request.SettlementWeightStage;
        entity.FEffectiveDate = request.EffectiveDate ?? entity.FEffectiveDate;
        entity.FPaymentMode = request.PaymentMode;
        entity.FPrepayRatio = request.PrepayRatio;
        entity.FBillingCycle = request.BillingCycle;
        entity.FBillingDay = request.BillingDay;
        entity.FPaymentDueDay = request.PaymentDueDay;
        entity.FThrowRatio = request.ThrowRatio;
        entity.FInsuranceRate = request.InsuranceRate;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;
        entity.FMatrixJson = PricingMatrixSerializer.Serialize(matrix);
        await _planRepo.UpdateAsync(entity);

        return (await GetByIdAsync(id))!;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        // 先按组织隔离确认报价归属当前组织（_planRepo.Query 受全局过滤器约束）
        var entity = await _planRepo.Query()
            .FirstOrDefaultAsync(e => e.FID == id);

        if (entity == null) return false;

        // 级联删除子表，避免外键约束导致删除失败或留下孤儿数据。
        // 这些子表无组织字段，靠父报价的组织过滤间接隔离（上面已确认父报价属当前组织）。
        var shops = await _dbContext.Set<ExpQuotationShop>().Where(s => s.FQuotationId == id).ToListAsync();
        var aliases = await _dbContext.Set<ExpQuotationAlias>().Where(a => a.FQuotationId == id).ToListAsync();
        var commissions = await _dbContext.Set<ExpQuotationCommission>().Where(c => c.FQuotationId == id).ToListAsync();
        var changeLogs = await _dbContext.Set<ExpQuotationChangeLog>().Where(l => l.FQuotationId == id).ToListAsync();
        var surchargeLinks = await _dbContext.Set<ExpQuotationSurchargeLink>().Where(l => l.F报价ID == id).ToListAsync();
        // 计费引擎的报价级附加费走 ExpPriceSurchargeScope(QUOTATION)，删除报价时同步清理，避免孤儿作用域
        var idStr = id.ToString();
        var surchargeScopes = await _dbContext.Set<ExpPriceSurchargeScope>()
            .Where(s => s.FLinkedType == "QUOTATION" && s.FLinkedId == idStr)
            .ToListAsync();

        _dbContext.Set<ExpQuotationShop>().RemoveRange(shops);
        _dbContext.Set<ExpQuotationAlias>().RemoveRange(aliases);
        _dbContext.Set<ExpQuotationCommission>().RemoveRange(commissions);
        _dbContext.Set<ExpQuotationChangeLog>().RemoveRange(changeLogs);
        _dbContext.Set<ExpQuotationSurchargeLink>().RemoveRange(surchargeLinks);
        _dbContext.Set<ExpPriceSurchargeScope>().RemoveRange(surchargeScopes);
        _dbContext.Set<ExpQuotation>().Remove(entity);

        // 单次 SaveChanges 在同一事务内提交，避免删一半失败
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<QuotationDto> CopyPlanAsync(long sourcePlanId)
    {
        var source = await _planRepo.Query()
            .FirstOrDefaultAsync(e => e.FID == sourcePlanId);

        if (source == null)
            throw new InvalidOperationException("源报价方案不存在");

        var newPlan = new ExpQuotation
        {
            FBrandCode = source.FBrandCode,
            FPlanName = source.FPlanName + " (副本)",
            FPlanCode = null, // 方案编号有唯一索引，副本不继承（原样复制会撞索引直接报错）
            FClientType = source.FClientType,
            FBusinessObjectId = source.FBusinessObjectId,
            FNetworkPointCode = source.FNetworkPointCode,
            FSharedShopEnabled = source.FSharedShopEnabled,
            F重量进位方式 = source.F重量进位方式,
            FSettlementWeightStage = source.FSettlementWeightStage,
            FEffectiveDate = source.FEffectiveDate,
            FPaymentMode = source.FPaymentMode,
            FPrepayRatio = source.FPrepayRatio,
            FBillingCycle = source.FBillingCycle,
            FBillingDay = source.FBillingDay,
            FPaymentDueDay = source.FPaymentDueDay,
            FThrowRatio = source.FThrowRatio,
            FInsuranceRate = source.FInsuranceRate,
            F含税 = source.F含税,
            F税率 = source.F税率,
            FRemark = source.FRemark,
            FStatus = 0, // 草稿
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now,
            FMatrixJson = source.FMatrixJson
        };

        // 事务保护：AddAsync 内部立即提交父行，子表在第二次 SaveChanges 提交，
        // 中途失败会留下无店铺/无佣金的半成品副本
        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        var savedPlan = await _planRepo.AddAsync(newPlan);

        // 复制子表（店铺/别名/佣金/加收关联）并重映射到新报价ID，避免复制后丢失配置
        var newId = savedPlan.FID;

        var srcShops = await _dbContext.Set<ExpQuotationShop>().Where(s => s.FQuotationId == sourcePlanId).ToListAsync();
        foreach (var s in srcShops)
            _dbContext.Set<ExpQuotationShop>().Add(new ExpQuotationShop
            {
                FQuotationId = newId,
                FShopName = s.FShopName,
                FCreatedTime = DateTime.Now
            });

        var srcAliases = await _dbContext.Set<ExpQuotationAlias>().Where(a => a.FQuotationId == sourcePlanId).ToListAsync();
        foreach (var a in srcAliases)
            _dbContext.Set<ExpQuotationAlias>().Add(new ExpQuotationAlias
            {
                FQuotationId = newId,
                FAlias = a.FAlias,
                FCreatedTime = DateTime.Now
            });

        var srcCommissions = await _dbContext.Set<ExpQuotationCommission>().Where(c => c.FQuotationId == sourcePlanId).ToListAsync();
        foreach (var c in srcCommissions)
            _dbContext.Set<ExpQuotationCommission>().Add(new ExpQuotationCommission
            {
                FQuotationId = newId,
                FEnabled = c.FEnabled,
                FCalcMethod = c.FCalcMethod,
                FRate = c.FRate,
                FFixedAmount = c.FFixedAmount,
                FWeightAmount = c.FWeightAmount,
                FTargetClientType = c.FTargetClientType,
                FTargetClientId = c.FTargetClientId,
                FRemark = c.FRemark,
                FCreatedTime = DateTime.Now
            });

        var srcLinks = await _dbContext.Set<ExpQuotationSurchargeLink>().Where(l => l.F报价ID == sourcePlanId).ToListAsync();
        foreach (var l in srcLinks)
            _dbContext.Set<ExpQuotationSurchargeLink>().Add(new ExpQuotationSurchargeLink
            {
                F报价ID = newId,
                F出港加收ID = l.F出港加收ID,
                F创建时间 = DateTime.Now
            });

        // 计费引擎的报价级附加费走 ExpPriceSurchargeScope(QUOTATION)，必须同步复制，
        // 否则副本启用后该报价的附加费静默丢失（ExpQuotationSurchargeLink 是无消费方的旧表）
        var srcIdStr = sourcePlanId.ToString();
        var srcScopes = await _dbContext.Set<ExpPriceSurchargeScope>()
            .Where(s => s.FLinkedType == "QUOTATION" && s.FLinkedId == srcIdStr)
            .ToListAsync();
        var newIdStr = newId.ToString();
        foreach (var scope in srcScopes)
            _dbContext.Set<ExpPriceSurchargeScope>().Add(new ExpPriceSurchargeScope
            {
                FSurchargeId = scope.FSurchargeId,
                FLinkedType = "QUOTATION",
                FLinkedId = newIdStr
            });

        await _dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        return (await GetByIdAsync(newId))!;
    }

    public async Task<PagedResult<ClientQuotationSummaryDto>> GetClientQuotationSummaryAsync(ClientQuotationSummaryQuery query)
    {
        var orgId = _orgContextAccessor.CurrentOrgId;

        // 构建各业务对象的 UNION ALL 子查询（带组织隔离）
        var unionParts = new List<string>();

        if (string.IsNullOrEmpty(query.Type) || query.Type == "KH")
        {
            var khSql = "SELECT F编号 AS Id, F简称 AS Name, F编号 AS Code, 'KH' AS Type FROM CRM客户";
            if (orgId.HasValue)
                khSql += $" WHERE (F组织ID = @orgId OR F组织ID = 0)";
            unionParts.Add(khSql);
        }

        if (string.IsNullOrEmpty(query.Type) || query.Type == "DL")
        {
            unionParts.Add("SELECT F编号 AS Id, F名称 AS Name, F编号 AS Code, 'DL' AS Type FROM EXP业务代理");
        }

        if (string.IsNullOrEmpty(query.Type) || query.Type == "WD")
        {
            var wdSql = "SELECT F编号 AS Id, F网点简称 AS Name, F编号 AS Code, 'WD' AS Type FROM EXP快递网点";
            if (orgId.HasValue)
                wdSql += $" WHERE (F所属组织ID = @orgId OR F所属组织ID = 0)";
            unionParts.Add(wdSql);
        }

        if (string.IsNullOrEmpty(query.Type) || query.Type == "CB")
        {
            var cbSql = "SELECT F编号 AS Id, F承包人 AS Name, F编号 AS Code, 'CB' AS Type FROM EXP承包区";
            if (orgId.HasValue)
                cbSql += $" WHERE (F所属组织ID = @orgId OR F所属组织ID = 0)";
            unionParts.Add(cbSql);
        }

        if (string.IsNullOrEmpty(query.Type) || query.Type == "YZ")
        {
            unionParts.Add("SELECT F编号 AS Id, F名称 AS Name, F编号 AS Code, 'YZ' AS Type FROM EXP末端驿站");
        }

        if (string.IsNullOrEmpty(query.Type) || query.Type == "YW")
        {
            unionParts.Add("SELECT F工号 AS Id, F姓名 AS Name, F工号 AS Code, 'YW' AS Type FROM EXP业务员");
        }

        if (unionParts.Count == 0)
        {
            return new PagedResult<ClientQuotationSummaryDto>
            {
                Items = new List<ClientQuotationSummaryDto>(),
                Total = 0,
                PageIndex = query.PageIndex,
                PageSize = query.PageSize
            };
        }

        var unionAll = string.Join(" UNION ALL ", unionParts);

        // 报价统计子查询（同样带组织隔离）
        var qcWhere = orgId.HasValue ? "WHERE (F组织ID = @orgId OR F组织ID = 0)" : "";
        var qcSql = $"SELECT F业务对象类型, F业务对象编号, COUNT(*) AS QuotationCount FROM EXP快递报价 {qcWhere} GROUP BY F业务对象类型, F业务对象编号";

        // 筛选条件
        var whereClauses = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            // 匹配业务对象名称/编号，或匹配旗下报价方案名称
            var quotationPlanWhere = orgId.HasValue
                ? "(F组织ID = @orgId OR F组织ID = 0) AND "
                : "";
            whereClauses.Add(
                $"(AllClients.Name LIKE '%' + @keyword + '%'" +
                $" OR AllClients.Code LIKE '%' + @keyword + '%'" +
                $" OR EXISTS (SELECT 1 FROM EXP快递报价" +
                $" WHERE {quotationPlanWhere}F业务对象类型 = AllClients.Type" +
                $" AND F业务对象编号 = AllClients.Id" +
                $" AND F方案名称 LIKE '%' + @keyword + '%'))");
        }
        if (!string.IsNullOrWhiteSpace(query.Type))
            whereClauses.Add("AllClients.Type = @type");
        if (query.HasQuotation == true)
            whereClauses.Add("ISNULL(QC.QuotationCount, 0) > 0");
        else if (query.HasQuotation == false)
            whereClauses.Add("ISNULL(QC.QuotationCount, 0) = 0");

        var whereStr = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        // 统计总数
        var countSql = $@"SELECT COUNT(*) FROM ({unionAll}) AS AllClients
LEFT JOIN ({qcSql}) AS QC ON QC.F业务对象类型 = AllClients.Type AND QC.F业务对象编号 = AllClients.Id
{whereStr}";

        // 分页查询
        var offset = (query.PageIndex - 1) * query.PageSize;
        var dataSql = $@"SELECT AllClients.Id, AllClients.Name, AllClients.Code, AllClients.Type,
    ISNULL(QC.QuotationCount, 0) AS QuotationCount
FROM ({unionAll}) AS AllClients
LEFT JOIN ({qcSql}) AS QC ON QC.F业务对象类型 = AllClients.Type AND QC.F业务对象编号 = AllClients.Id
{whereStr}
ORDER BY AllClients.Type, AllClients.Name
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        // 构建参数
        var parameters = new List<SqlParameter>
        {
            new SqlParameter("@offset", offset),
            new SqlParameter("@pageSize", query.PageSize)
        };
        if (orgId.HasValue)
            parameters.Add(new SqlParameter("@orgId", orgId.Value));
        if (!string.IsNullOrWhiteSpace(query.Keyword))
            parameters.Add(new SqlParameter("@keyword", query.Keyword.Trim()));
        if (!string.IsNullOrWhiteSpace(query.Type))
            parameters.Add(new SqlParameter("@type", query.Type.Trim()));

        var conn = _dbContext.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            // 查询总数
            using var countCmd = conn.CreateCommand();
            countCmd.CommandText = countSql;
            foreach (var p in parameters.Where(p => p.ParameterName != "@offset" && p.ParameterName != "@pageSize"))
                countCmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
            var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            // 查询数据
            using var dataCmd = conn.CreateCommand();
            dataCmd.CommandText = dataSql;
            foreach (var p in parameters)
                dataCmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));

            var items = new List<ClientQuotationSummaryDto>();
            using var reader = await dataCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new ClientQuotationSummaryDto
                {
                    Id = reader.GetString(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Code = reader.GetString(2),
                    Type = reader.GetString(3),
                    QuotationCount = reader.GetInt32(4)
                });
            }

            return new PagedResult<ClientQuotationSummaryDto>
            {
                Items = items,
                Total = totalCount,
                PageIndex = query.PageIndex,
                PageSize = query.PageSize
            };
        }
        finally
        {
            if (conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    private static readonly Dictionary<string, string> ClientTypeNames = new()
    {
        ["KH"] = "客户",
        ["DL"] = "代理",
        ["WD"] = "网点",
        ["CB"] = "承包区",
        ["YZ"] = "驿站",
        ["YW"] = "业务员",
    };

    public async Task<List<QuotationByShopGroupDto>> GetQuotationsByShopAsync(string shopName)
    {
        // 1. 通过 _planRepo.Query() 查询（自动应用IOrgScoped全局过滤器）
        // 2. Join ExpQuotationShop 筛选 FShopName == shopName
        var quotationIds = await _dbContext.Set<ExpQuotationShop>()
            .Where(s => s.FShopName == shopName)
            .Select(s => s.FQuotationId)
            .Distinct()
            .ToListAsync();

        if (quotationIds.Count == 0)
            return new List<QuotationByShopGroupDto>();

        // 3. 取最多200条报价（先按FEffectiveDate DESC排序再Take）
        var items = await _planRepo.Query()
            .Where(q => quotationIds.Contains(q.FID))
            .OrderByDescending(q => q.FEffectiveDate)
            .ThenByDescending(q => q.FCreatedTime)
            .Take(200)
            .ToListAsync();

        if (items.Count == 0)
            return new List<QuotationByShopGroupDto>();

        // 5. 批量加载品牌名称
        var brandCodes = items.Select(e => e.FBrandCode).Distinct().ToList();
        var brandNames = await _brandRepo.Query()
            .Where(b => brandCodes.Contains(b.FCode))
            .ToDictionaryAsync(b => b.FCode, b => b.FName);

        // 6. 批量加载店铺数量
        var planIds = items.Select(e => e.FID).ToList();
        var shopCounts = await _dbContext.Set<ExpQuotationShop>()
            .Where(s => planIds.Contains(s.FQuotationId))
            .GroupBy(s => s.FQuotationId)
            .Select(g => new { QuotationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.QuotationId, x => x.Count);

        // 7. 批量解析业务对象名称
        // 收集所有出现的 (ClientType, ClientId) 对（排除空值）
        var clientPairs = items
            .Where(e => !string.IsNullOrEmpty(e.FClientType) && !string.IsNullOrEmpty(e.FBusinessObjectId))
            .Select(e => (Type: e.FClientType!, Id: e.FBusinessObjectId!))
            .Distinct()
            .ToList();

        var clientNameMap = await ResolveClientNamesAsync(clientPairs);

        // 4. 按 FClientType + FBusinessObjectId 在内存中分组
        // 8. 对 FClientType 或 FBusinessObjectId 为空的报价，归入特殊分组
        var groups = items
            .GroupBy(e =>
            {
                if (string.IsNullOrEmpty(e.FClientType) || string.IsNullOrEmpty(e.FBusinessObjectId))
                    return (Type: "", Id: "");
                return (Type: e.FClientType, Id: e.FBusinessObjectId);
            })
            .Select(g =>
            {
                var isUnassigned = string.IsNullOrEmpty(g.Key.Type) || string.IsNullOrEmpty(g.Key.Id);
                var clientType = isUnassigned ? null : g.Key.Type;
                var clientId = isUnassigned ? null : g.Key.Id;
                var clientTypeName = isUnassigned
                    ? "未分配"
                    : ClientTypeNames.GetValueOrDefault(g.Key.Type, g.Key.Type);
                var clientName = isUnassigned
                    ? "未分配业务对象"
                    : clientNameMap.TryGetValue((g.Key.Type, g.Key.Id), out var name)
                        ? name
                        : g.Key.Id;

                return new QuotationByShopGroupDto
                {
                    ClientType = clientType,
                    ClientTypeName = clientTypeName,
                    ClientId = clientId,
                    ClientName = clientName,
                    Quotations = g.Select(e => MapToListItemDto(e, brandNames, shopCounts)).ToList()
                };
            })
            .ToList();

        return groups;
    }

    /// <summary>
    /// 批量解析业务对象名称（UNION ALL 模式）
    /// </summary>
    private async Task<Dictionary<(string Type, string Id), string>> ResolveClientNamesAsync(
        List<(string Type, string Id)> clientPairs)
    {
        var result = new Dictionary<(string Type, string Id), string>();
        if (clientPairs.Count == 0)
            return result;

        var orgId = _orgContextAccessor.CurrentOrgId;

        // 业务对象编号拼入 SQL 前转义单引号，防止注入/破坏语句
        static string Quote(string s) => "'" + (s ?? string.Empty).Replace("'", "''") + "'";

        // 按类型分组，只构建实际出现的类型的 UNION
        var typesPresent = clientPairs.Select(p => p.Type).Distinct().ToList();
        var unionParts = new List<string>();

        if (typesPresent.Contains("KH"))
        {
            var khIds = clientPairs.Where(p => p.Type == "KH").Select(p => Quote(p.Id));
            var khIn = string.Join(",", khIds);
            var khSql = $"SELECT F编号 AS Id, F简称 AS Name, 'KH' AS Type FROM CRM客户 WHERE F编号 IN ({khIn})";
            if (orgId.HasValue)
                khSql += " AND (F组织ID = @orgId OR F组织ID = 0)";
            unionParts.Add(khSql);
        }

        if (typesPresent.Contains("DL"))
        {
            var dlIds = clientPairs.Where(p => p.Type == "DL").Select(p => Quote(p.Id));
            var dlIn = string.Join(",", dlIds);
            unionParts.Add($"SELECT F编号 AS Id, F名称 AS Name, 'DL' AS Type FROM EXP业务代理 WHERE F编号 IN ({dlIn})");
        }

        if (typesPresent.Contains("WD"))
        {
            var wdIds = clientPairs.Where(p => p.Type == "WD").Select(p => Quote(p.Id));
            var wdIn = string.Join(",", wdIds);
            var wdSql = $"SELECT F编号 AS Id, F网点简称 AS Name, 'WD' AS Type FROM EXP快递网点 WHERE F编号 IN ({wdIn})";
            if (orgId.HasValue)
                wdSql += " AND (F所属组织ID = @orgId OR F所属组织ID = 0)";
            unionParts.Add(wdSql);
        }

        if (typesPresent.Contains("CB"))
        {
            var cbIds = clientPairs.Where(p => p.Type == "CB").Select(p => Quote(p.Id));
            var cbIn = string.Join(",", cbIds);
            var cbSql = $"SELECT F编号 AS Id, F承包人 AS Name, 'CB' AS Type FROM EXP承包区 WHERE F编号 IN ({cbIn})";
            if (orgId.HasValue)
                cbSql += " AND (F所属组织ID = @orgId OR F所属组织ID = 0)";
            unionParts.Add(cbSql);
        }

        if (typesPresent.Contains("YZ"))
        {
            var yzIds = clientPairs.Where(p => p.Type == "YZ").Select(p => Quote(p.Id));
            var yzIn = string.Join(",", yzIds);
            unionParts.Add($"SELECT F编号 AS Id, F名称 AS Name, 'YZ' AS Type FROM EXP末端驿站 WHERE F编号 IN ({yzIn})");
        }

        if (typesPresent.Contains("YW"))
        {
            var ywIds = clientPairs.Where(p => p.Type == "YW").Select(p => Quote(p.Id));
            var ywIn = string.Join(",", ywIds);
            unionParts.Add($"SELECT F工号 AS Id, F姓名 AS Name, 'YW' AS Type FROM EXP业务员 WHERE F工号 IN ({ywIn})");
        }

        if (unionParts.Count == 0)
            return result;

        var unionAll = string.Join(" UNION ALL ", unionParts);
        var conn = _dbContext.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = unionAll;
            if (orgId.HasValue)
                cmd.Parameters.Add(new SqlParameter("@orgId", orgId.Value));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.IsDBNull(0) ? "" : reader.GetString(0);
                var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var type = reader.IsDBNull(2) ? "" : reader.GetString(2);
                result[(type, id)] = name;
            }
        }
        finally
        {
            if (conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }

        return result;
    }

    /// <summary>
    /// 校验矩阵完整性（所有省份是否都有报价）
    /// </summary>
    private async Task ValidateMatrixCompleteness(List<WeightSegmentInput> segments, bool allowIncomplete)
    {
        if (allowIncomplete) return; // 用户已确认，跳过校验
        if (segments.Count == 0) return;

        // 从数据库加载所有省份ID
        var allProvinceIds = await _provinceRepo.Query()
            .Select(p => p.FID)
            .ToListAsync();

        var gaps = new List<string>();

        foreach (var seg in segments.OrderBy(s => s.SegmentIndex))
        {
            var cellsWithPrice = seg.Cells
                .Where(c => HasValidPrice(c, seg.PricingMethod))
                .Select(c => c.ProvinceId)
                .ToHashSet();
            var actualMissing = allProvinceIds.Except(cellsWithPrice).ToList();

            if (actualMissing.Any())
            {
                var segLabel = seg.WeightTo.HasValue
                    ? $"{seg.WeightFrom}-{seg.WeightTo}kg"
                    : $"{seg.WeightFrom}kg以上";
                gaps.Add($"【{segLabel}】缺少{actualMissing.Count}个省份的报价");
            }
        }

        if (gaps.Any())
        {
            throw new InvalidOperationException(
                $"报价矩阵未完整填写：{string.Join("；", gaps)}");
        }
    }

    private static bool HasValidPrice(PriceCellInput cell, int pricingMethod)
    {
        // A3' 统一后以 BasePrice + ContinuePrice 判断
        // mode=1：BasePrice 必填，ContinuePrice 应为空
        // mode=3：BasePrice 必填（首重价），ContinuePrice 可选
        return cell.BasePrice > 0m;
    }

    /// <summary>
    /// 归一化 + 段-cell mode 强制重算：
    ///   1) cell.BasePrice == 0 且 ContinuePrice 为空 → 该 cell 仍保留（后续校验）
    ///   2) cont=0 归一为 null
    ///   3) 任一 cell.ContinuePrice 非空 → 段 PricingMethod=3；全为空 → mode=1
    ///   4) mode=1 时段 FirstWeight/ContinueWeight 置空
    ///   5) mode=3 时首重重量允许为0（如"0+15"合法）
    /// </summary>
    private static void NormalizeSegmentsAndCells(List<WeightSegmentInput> segments)
    {
        foreach (var seg in segments)
        {
            // cont=0 归一为 null
            foreach (var cell in seg.Cells)
            {
                if (cell.ContinuePrice.HasValue && cell.ContinuePrice.Value == 0m)
                    cell.ContinuePrice = null;
            }

            var anyContinue = seg.Cells.Any(c => c.ContinuePrice.HasValue);
            seg.PricingMethod = anyContinue ? 3 : 1;

            if (seg.PricingMethod == 1)
            {
                seg.FirstWeight = null;
                seg.ContinueWeight = null;
            }
            // mode=3 时不强制 FirstWeight > 0，允许首重为0
        }
    }

    /// <summary>
    /// 校验段连续性（段之间无间隙无重叠）
    /// </summary>
    private static void ValidateSegments(List<WeightSegmentInput> segments)
    {
        if (segments.Count == 0) return;

        var sorted = segments.OrderBy(s => s.SegmentIndex).ToList();
        for (int i = 1; i < sorted.Count; i++)
        {
            var prev = sorted[i - 1];
            var curr = sorted[i];
            if (prev.WeightTo.HasValue && curr.WeightFrom.HasValue && prev.WeightTo != curr.WeightFrom)
                throw new InvalidOperationException($"重量段 {prev.SegmentIndex} 和 {curr.SegmentIndex} 之间存在间隙或重叠");
        }
    }

    /// <summary>
    /// 从前端提交的段列表构建 PricingMatrix
    /// </summary>
    private static PricingMatrix BuildMatrixFromRequest(List<WeightSegmentInput> segments)
    {
        return new PricingMatrix
        {
            Segments = segments.Select((s, idx) => new PricingSegment
            {
                SegmentIndex = s.SegmentIndex > 0 ? s.SegmentIndex : idx + 1,
                WeightFrom = s.WeightFrom,
                WeightTo = s.WeightTo,
                RoundingMethod = s.RoundingMethod,
                TruncParam = s.TruncParam,
                CeilParam = s.CeilParam ?? s.RoundingParam,
                Cells = s.Cells.Select(c => new PricingCell
                {
                    ProvinceId = c.ProvinceId,
                    BasePrice = c.BasePrice,
                    ContinuePrice = c.ContinuePrice ?? 0m,
                    FirstWeight = c.FirstWeight ?? c.FirstWeightOverride ?? s.FirstWeight ?? 0m,
                    ContinueStep = c.ContinueStep ?? c.ContinueStepOverride ?? s.ContinueWeight ?? 1m,
                    RoundingMethodOverride = c.RoundingMethodOverride,
                    TruncParamOverride = c.TruncParamOverride,
                    CeilParamOverride = c.CeilParamOverride ?? c.RoundingParamOverride
                }).ToList()
            }).ToList()
        };
    }

    private static QuotationDto MapToDto(ExpQuotation e, string? brandName = null)
    {
        var matrix = PricingMatrixSerializer.Deserialize(e.FMatrixJson);

        return new QuotationDto
        {
            Id = e.FID,
            BrandCode = e.FBrandCode,
            BrandName = brandName,
            PlanName = e.FPlanName,
            PlanCode = e.FPlanCode,
            NetworkPointCode = e.FNetworkPointCode,
            ClientType = e.FClientType,
            ClientId = e.FBusinessObjectId,
            SharedShopEnabled = e.FSharedShopEnabled,
            WeightRoundingMethod = e.F重量进位方式,
            SettlementWeightStage = e.FSettlementWeightStage,
            Status = e.FStatus,
            Version = 1,
            OaProcessId = e.FOaProcessId,
            PreviousPlanId = null,
            ApprovedBy = e.FApprovedBy,
            ApprovedAt = e.FApprovedAt,
            EffectiveDate = e.FEffectiveDate,
            PaymentMode = e.FPaymentMode,
            PrepayRatio = e.FPrepayRatio,
            BillingCycle = e.FBillingCycle,
            BillingDay = e.FBillingDay,
            PaymentDueDay = e.FPaymentDueDay,
            ThrowRatio = e.FThrowRatio,
            InsuranceRate = e.FInsuranceRate,
            Remark = e.FRemark,
            CreatedTime = e.FCreatedTime,
            UpdatedTime = e.FUpdatedTime,
            Segments = matrix.Segments
                .OrderBy(s => s.SegmentIndex)
                .Select(s => new WeightSegmentDto
                {
                    Id = s.SegmentIndex,
                    SegmentIndex = s.SegmentIndex,
                    WeightFrom = s.WeightFrom,
                    WeightTo = s.WeightTo,
                    PricingMethod = s.Cells.Any(c => c.ContinuePrice != 0m) ? 3 : 1,
                    FirstWeight = s.Cells.FirstOrDefault()?.FirstWeight,
                    ContinueWeight = s.Cells.FirstOrDefault()?.ContinueStep,
                    RoundingMethod = s.RoundingMethod,
                    TruncParam = s.TruncParam,
                    CeilParam = s.CeilParam,
                    RoundingParam = s.CeilParam
                }).ToList(),
            Cells = matrix.Segments
                .SelectMany(s => s.Cells.Select(c => new PriceCellDto
                {
                    Id = 0,
                    SegmentId = s.SegmentIndex,
                    ProvinceId = c.ProvinceId,
                    BasePrice = c.BasePrice,
                    ContinuePrice = c.ContinuePrice != 0m ? c.ContinuePrice : (decimal?)null,
                    FirstWeight = c.FirstWeight,
                    ContinueStep = c.ContinueStep,
                    RoundingMethodOverride = c.RoundingMethodOverride,
                    TruncParamOverride = c.TruncParamOverride,
                    CeilParamOverride = c.CeilParamOverride,
                    FirstWeightOverride = c.FirstWeight != 0 ? c.FirstWeight : (decimal?)null,
                    ContinueStepOverride = c.ContinueStep != 1 ? c.ContinueStep : (decimal?)null,
                    RoundingParamOverride = c.CeilParamOverride
                })).ToList()
        };
    }

    private static QuotationListItemDto MapToListItemDto(
        ExpQuotation e,
        Dictionary<string, string>? brandNames = null,
        Dictionary<long, int>? shopCounts = null) => new()
    {
        Id = e.FID,
        BrandCode = e.FBrandCode,
        BrandName = brandNames != null && brandNames.TryGetValue(e.FBrandCode, out var bn) ? bn : null,
        PlanName = e.FPlanName,
        PlanCode = e.FPlanCode,
        NetworkPointCode = e.FNetworkPointCode,
        ClientType = e.FClientType,
        ClientId = e.FBusinessObjectId,
        EffectiveDate = e.FEffectiveDate,
        SharedShopEnabled = e.FSharedShopEnabled,
        SettlementWeightStage = e.FSettlementWeightStage,
        Status = e.FStatus,
        Version = 1,
        CreatedTime = e.FCreatedTime,
        UpdatedTime = e.FUpdatedTime,
        ShopCount = shopCounts != null && shopCounts.TryGetValue(e.FID, out var sc) ? sc : 0
    };
}
