using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Express.Services.Billing;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 计费服务实现
/// </summary>
public class BillingService : IBillingService
{
    private readonly PricingEngine _engine;
    private readonly IRepository<ExpBillingResult> _resultRepo;
    private readonly IRepository<ExpBillingCostBreakdown> _costBreakdownRepo;
    private readonly IRepository<ExpWaybill> _waybillRepo;
    private readonly IRepository<ExpQuotation> _quotationRepo;
    private readonly IRepository<ExpCostItem> _costItemRepo;
    private readonly IRepository<ExpCostPlanItem> _costPlanItemRepo;
    private readonly IRepository<ExpBrand> _brandRepo;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BillingService(
        PricingEngine engine,
        IRepository<ExpBillingResult> resultRepo,
        IRepository<ExpBillingCostBreakdown> costBreakdownRepo,
        IRepository<ExpWaybill> waybillRepo,
        IRepository<ExpQuotation> quotationRepo,
        IRepository<ExpCostItem> costItemRepo,
        IRepository<ExpCostPlanItem> costPlanItemRepo,
        IRepository<ExpBrand> brandRepo,
        IHttpContextAccessor httpContextAccessor)
    {
        _engine = engine;
        _resultRepo = resultRepo;
        _costBreakdownRepo = costBreakdownRepo;
        _waybillRepo = waybillRepo;
        _quotationRepo = quotationRepo;
        _costItemRepo = costItemRepo;
        _costPlanItemRepo = costPlanItemRepo;
        _brandRepo = brandRepo;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<BillingExecutionResult> ExecuteBillingAsync(
        List<BillingWaybillData> waybills, string sourceTable, long batchId, string resultTable)
    {
        return await _engine.ExecuteAsync(waybills, sourceTable, batchId, resultTable);
    }

    public async Task<PagedResult<BillingResultListItemDto>> GetResultListAsync(BillingResultQueryRequest request)
    {
        var query = _resultRepo.Query().AsQueryable();

        // 多网点视角过滤
        var orgId = (long)(_httpContextAccessor.HttpContext?.Items["CurrentOrgId"] ?? 0L);
        if (orgId > 0)
            query = query.Where(r => r.FNetworkPointCode == orgId.ToString());

        if (request.WaybillDate.HasValue)
            query = query.Where(r => r.FWaybillDate == request.WaybillDate);
        if (!string.IsNullOrWhiteSpace(request.PartyClientId))
            query = query.Where(r => r.FPartyClientId == request.PartyClientId);
        if (!string.IsNullOrWhiteSpace(request.BrandCode))
            query = query.Where(r => r.FBrandCode == request.BrandCode);
        if (request.CalcStatus.HasValue)
            query = query.Where(r => r.FCalcStatus == request.CalcStatus);
        if (request.PartyRole.HasValue)
            query = query.Where(r => r.FPartyRole == request.PartyRole);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.FID)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new BillingResultListItemDto
            {
                Id = r.FID,
                WaybillNo = r.FWaybillNo,
                WaybillDate = r.FWaybillDate,
                PartyRole = r.FPartyRole,
                ChargeAmount = r.FChargeAmount,
                CalcStatus = r.FCalcStatus,
                ErrorMessage = r.FErrorMessage
            })
            .ToListAsync();

        // 补充客户名（通过报价方案获取）
        var quotationIds = await query
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Where(r => r.FQuotationId != null)
            .Select(r => r.FQuotationId!.Value)
            .Distinct()
            .ToListAsync();
        var quotationMap = await _quotationRepo.Query()
            .Where(q => quotationIds.Contains(q.FID))
            .Select(q => new { q.FID, q.FPlanName })
            .ToDictionaryAsync(q => q.FID, q => q.FPlanName);

        // 获取对应的 QuotationId 用于名称映射
        var resultIds = items.Select(i => i.Id).ToList();
        var resultQuotationMap = await _resultRepo.Query()
            .Where(r => resultIds.Contains(r.FID))
            .Where(r => r.FQuotationId != null)
            .Select(r => new { r.FID, QuotationId = r.FQuotationId!.Value })
            .ToDictionaryAsync(r => r.FID, r => r.QuotationId);

        foreach (var item in items)
        {
            if (resultQuotationMap.TryGetValue(item.Id, out var qid))
                item.PartyClientName = quotationMap.GetValueOrDefault(qid);
        }

        return new PagedResult<BillingResultListItemDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<BillingResultDto?> GetResultByIdAsync(long id)
    {
        var result = await _resultRepo.GetByIdAsync(id);
        if (result == null) return null;

        return await MapToDto(result);
    }

    public async Task<List<BillingResultDto>> GetResultsByWaybillAsync(long waybillId)
    {
        // 先查运单编号
        var waybill = await _waybillRepo.GetByIdAsync(waybillId);
        if (waybill == null) return new();

        var results = await _resultRepo.Query()
            .Where(r => r.FWaybillNo == waybill.FWaybillNo)
            .OrderBy(r => r.FChainLevel)
            .ToListAsync();

        var dtos = new List<BillingResultDto>();
        foreach (var r in results)
        {
            dtos.Add(await MapToDto(r));
        }
        return dtos;
    }

    // ===== 异常运单处理 =====

    /// <summary>错误码→中文名称映射</summary>
    private static readonly Dictionary<string, string> ErrorCodeNames = new()
    {
        ["ERR_SHOP_NO_ASSIGNMENT"] = "店铺未分配",
        ["ERR_NO_PRICE_CELL"] = "无报价",
        ["ERR_NO_PRICE_PLAN"] = "无报价方案",
        ["ERR_NO_NETWORK_POINT"] = "链条无网点",
        ["ERR_NO_COST_PLAN_FOR_NP"] = "网点无成本方案",
        ["ERR_ALIAS_NOT_FOUND"] = "客户别名未匹配",
        ["ERR_NO_WEIGHT"] = "无有效重量",
        ["ERR_CLIENT_NOT_FOUND"] = "业务对象不存在",
        ["ERR_UNKNOWN"] = "未知异常",
        ["ERR_NETWORK_POINT_NOT_FOUND"] = "未识别网点"
    };

    /// <summary>从 FErrorMessage 中提取错误码（格式: "ERR_XXX: message"）</summary>
    private static string ExtractErrorCode(string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage)) return "ERR_UNKNOWN";
        var match = Regex.Match(errorMessage, @"^(ERR_\w+):");
        return match.Success ? match.Groups[1].Value : "ERR_UNKNOWN";
    }

    public async Task<BillingErrorStatsDto> GetErrorStatsAsync(string? brandCode)
    {
        // 多网点视角过滤
        var orgId = (long)(_httpContextAccessor.HttpContext?.Items["CurrentOrgId"] ?? 0L);

        // 来源1：FBillingStatus=2 的运单（完全无法计费）
        var errorWaybillQuery = _waybillRepo.Query().Where(w => w.FBillingStatus == 2);
        if (!string.IsNullOrWhiteSpace(brandCode))
            errorWaybillQuery = errorWaybillQuery.Where(w => w.FBrandCode == brandCode);

        var errorWaybills = await errorWaybillQuery
            .Select(w => new { w.FID, w.FWaybillNo, w.FShopName, w.FBrandCode, w.FWaybillDate })
            .ToListAsync();

        // 来源2：有异常计费结果的运单（FCalcStatus=2）
        var errorResultQuery = _resultRepo.Query().Where(r => r.FCalcStatus == 2);
        if (orgId > 0)
            errorResultQuery = errorResultQuery.Where(r => r.FNetworkPointCode == orgId.ToString());
        if (!string.IsNullOrWhiteSpace(brandCode))
            errorResultQuery = errorResultQuery.Where(r => r.FBrandCode == brandCode);

        var errorResults = await errorResultQuery
            .Select(r => new { r.FWaybillNo, r.FErrorMessage, r.FWaybillDate })
            .ToListAsync();

        // 获取来源2对应的运单信息
        var resultWaybillNos = errorResults.Select(r => r.FWaybillNo).Where(n => n != null).Distinct().ToList();
        var resultWaybillMap = resultWaybillNos.Count > 0
            ? await _waybillRepo.Query()
                .Where(w => resultWaybillNos.Contains(w.FWaybillNo))
                .Select(w => new { w.FWaybillNo, w.FShopName, w.FBrandCode })
                .ToDictionaryAsync(w => w.FWaybillNo)
            : new();

        // 合并两个来源为统一结构
        var allErrors = new List<(long WaybillId, string ErrorCode, string ShopName, DateTime? WaybillDate)>();

        // 来源1：从已有异常计费结果中获取错误码
        var waybillNosFromSource1 = new HashSet<string>(errorWaybills.Select(w => w.FWaybillNo));
        var source1ResultMap = errorResults
            .Where(r => r.FWaybillNo != null && waybillNosFromSource1.Contains(r.FWaybillNo))
            .GroupBy(r => r.FWaybillNo!)
            .ToDictionary(g => g.Key, g => g.First().FErrorMessage);

        foreach (var w in errorWaybills)
        {
            var errMsg = source1ResultMap.GetValueOrDefault(w.FWaybillNo);
            var code = ExtractErrorCode(errMsg);
            allErrors.Add((w.FID, code, w.FShopName, w.FWaybillDate));
        }

        // 来源2：不在来源1中的
        foreach (var r in errorResults)
        {
            if (r.FWaybillNo != null && waybillNosFromSource1.Contains(r.FWaybillNo)) continue;
            var code = ExtractErrorCode(r.FErrorMessage);
            var shopName = r.FWaybillNo != null && resultWaybillMap.TryGetValue(r.FWaybillNo, out var wInfo) ? wInfo.FShopName : "";
            allErrors.Add((0, code, shopName, r.FWaybillDate));
        }

        // 按运单ID去重（同一运单可能有多条异常结果）
        var distinctErrors = allErrors
            .GroupBy(e => e.WaybillId)
            .Select(g => g.First())
            .ToList();

        // 按 ErrorCode 分组统计
        var groups = distinctErrors
            .GroupBy(e => e.ErrorCode)
            .Select(g => new BillingErrorGroupDto
            {
                ErrorCode = g.Key,
                ErrorName = ErrorCodeNames.GetValueOrDefault(g.Key, g.Key),
                WaybillCount = g.Count(),
                ShopNames = g.Select(e => e.ShopName).Where(s => !string.IsNullOrEmpty(s)).Distinct().Take(50).ToList(),
                DateRange = new BillingErrorDateRange
                {
                    From = g.Min(e => e.WaybillDate),
                    To = g.Max(e => e.WaybillDate)
                }
            })
            .OrderByDescending(g => g.WaybillCount)
            .ToList();

        return new BillingErrorStatsDto
        {
            Groups = groups,
            TotalErrorWaybills = distinctErrors.Count
        };
    }

    public async Task<PagedResult<BillingErrorDetailItemDto>> GetErrorDetailAsync(BillingErrorDetailRequest request)
    {
        var errorCode = request.ErrorCode;
        var likePattern = $"{errorCode}:%";

        // 多网点视角过滤
        var orgId = (long)(_httpContextAccessor.HttpContext?.Items["CurrentOrgId"] ?? 0L);

        // 来源1：FBillingStatus=2 的运单，关联计费结果中错误码匹配
        // 来源2：FCalcStatus=2 的计费结果
        var errorResultsQuery = _resultRepo.Query()
            .Where(r => r.FCalcStatus == 2 && r.FErrorMessage != null && r.FErrorMessage.StartsWith(errorCode + ":"));
        if (orgId > 0)
            errorResultsQuery = errorResultsQuery.Where(r => r.FNetworkPointCode == orgId.ToString());

        var matchedWaybillNos = await errorResultsQuery
            .Select(r => r.FWaybillNo)
            .Where(n => n != null)
            .Distinct()
            .ToListAsync();

        // 同时包含 FBillingStatus=2 但可能无计费结果的运单
        var waybillQuery = _waybillRepo.Query()
            .Where(w => matchedWaybillNos.Contains(w.FWaybillNo) || (w.FBillingStatus == 2 && matchedWaybillNos.Contains(w.FWaybillNo)));

        // 如果是 ERR_UNKNOWN 类型，还要包含没有匹配到具体错误码的
        if (errorCode == "ERR_UNKNOWN")
        {
            var knownCodes = ErrorCodeNames.Keys.Where(k => k != "ERR_UNKNOWN").ToList();
            var knownWaybillIdsQuery = _resultRepo.Query()
                .Where(r => r.FCalcStatus == 2 && r.FErrorMessage != null);
            if (orgId > 0)
                knownWaybillIdsQuery = knownWaybillIdsQuery.Where(r => r.FNetworkPointCode == orgId.ToString());
            var knownWaybillResults = await knownWaybillIdsQuery
                .Select(r => new { r.FWaybillNo, r.FErrorMessage })
                .ToListAsync();

            var unknownWaybillNos = knownWaybillResults
                .Where(r => r.FWaybillNo != null && !knownCodes.Any(k => r.FErrorMessage!.StartsWith(k + ":")))
                .Select(r => r.FWaybillNo!)
                .Distinct()
                .ToList();

            // 也包含 FBillingStatus=2 但完全没有计费结果的
            var allErrorResultQuery = _resultRepo.Query()
                .Where(r => r.FCalcStatus == 2);
            if (orgId > 0)
                allErrorResultQuery = allErrorResultQuery.Where(r => r.FNetworkPointCode == orgId.ToString());
            var allErrorResultWaybillNos = await allErrorResultQuery
                .Select(r => r.FWaybillNo)
                .Where(n => n != null)
                .Distinct()
                .ToListAsync();

            var noResultWaybillQuery = _waybillRepo.Query()
                .Where(w => w.FBillingStatus == 2 && !allErrorResultWaybillNos.Contains(w.FWaybillNo));
            var noResultWaybillNos = await noResultWaybillQuery
                .Select(w => w.FWaybillNo)
                .Where(n => n != null)
                .ToListAsync();

            matchedWaybillNos = unknownWaybillNos.Cast<string?>().Union(noResultWaybillNos).Distinct().ToList();
            waybillQuery = _waybillRepo.Query().Where(w => matchedWaybillNos.Contains(w.FWaybillNo));
        }

        var total = await waybillQuery.CountAsync();

        // 获取品牌名称映射
        var brandMap = await _brandRepo.Query()
            .Select(b => new { b.FCode, b.FName })
            .ToDictionaryAsync(b => b.FCode, b => b.FName);

        // 获取运单的错误信息
        Dictionary<string, string?> errorMsgMap;
        if (errorCode == "ERR_UNKNOWN")
        {
            // ERR_UNKNOWN 场景：从所有 FCalcStatus=2 且不以已知错误码开头的记录中构建
            var knownPrefixes = ErrorCodeNames.Keys.Where(k => k != "ERR_UNKNOWN").ToList();
            var unknownMsgQuery = _resultRepo.Query()
                .Where(r => r.FCalcStatus == 2 && r.FErrorMessage != null);
            if (orgId > 0)
                unknownMsgQuery = unknownMsgQuery.Where(r => r.FNetworkPointCode == orgId.ToString());
            var unknownMsgList = await unknownMsgQuery
                .Select(r => new { r.FWaybillNo, r.FErrorMessage })
                .ToListAsync();
            errorMsgMap = unknownMsgList
                .Where(r => r.FWaybillNo != null && !knownPrefixes.Any(k => r.FErrorMessage!.StartsWith(k + ":")))
                .GroupBy(r => r.FWaybillNo!)
                .ToDictionary(g => g.Key, g => g.First().FErrorMessage);
        }
        else
        {
            errorMsgMap = await errorResultsQuery
                .Where(r => r.FWaybillNo != null)
                .GroupBy(r => r.FWaybillNo!)
                .Select(g => new { WaybillNo = g.Key, ErrorMessage = g.First().FErrorMessage })
                .ToDictionaryAsync(x => x.WaybillNo, x => x.ErrorMessage);
        }

        var waybills = await waybillQuery
            .OrderByDescending(w => w.FWaybillDate)
            .ThenByDescending(w => w.FID)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(w => new { w.FID, w.FWaybillNo, w.FShopName, w.FBrandCode, w.FWaybillDate })
            .ToListAsync();

        var items = waybills.Select(w => new BillingErrorDetailItemDto
        {
            WaybillId = w.FID,
            WaybillNo = w.FWaybillNo,
            ShopName = w.FShopName,
            BrandName = brandMap.GetValueOrDefault(w.FBrandCode),
            WaybillDate = w.FWaybillDate,
            ErrorCode = errorCode,
            ErrorMessage = errorMsgMap.GetValueOrDefault(w.FWaybillNo)
        }).ToList();

        return new PagedResult<BillingErrorDetailItemDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<BillingRetryResultDto> RetryBillingAsync(BillingRetryRequest request)
    {
        if (string.IsNullOrEmpty(request.ErrorCode)
            && (request.ShopNames == null || request.ShopNames.Count == 0)
            && (request.WaybillIds == null || request.WaybillIds.Count == 0))
        {
            return new BillingRetryResultDto { Message = "至少提供一个筛选条件" };
        }

        // Step 1: 查找匹配的异常运单
        var waybillQuery = _waybillRepo.Query().Where(w => w.FBillingStatus == 2);

        if (request.WaybillIds is { Count: > 0 })
        {
            waybillQuery = waybillQuery.Where(w => request.WaybillIds.Contains(w.FID));
        }
        else
        {
            if (request.ShopNames is { Count: > 0 })
                waybillQuery = waybillQuery.Where(w => request.ShopNames.Contains(w.FShopName));

            if (!string.IsNullOrEmpty(request.ErrorCode))
            {
                // 需要通过计费结果找到对应的运单
                var errorWaybillNos = await _resultRepo.Query()
                    .Where(r => r.FCalcStatus == 2 && r.FErrorMessage != null
                        && r.FErrorMessage.StartsWith(request.ErrorCode + ":"))
                    .Select(r => r.FWaybillNo)
                    .Where(n => n != null)
                    .Distinct()
                    .ToListAsync();

                // 合并：直接 FBillingStatus=2 的运单 + 有异常计费结果的运单
                waybillQuery = _waybillRepo.Query()
                    .Where(w => w.FBillingStatus == 2 && errorWaybillNos.Contains(w.FWaybillNo));

                if (request.ShopNames is { Count: > 0 })
                    waybillQuery = waybillQuery.Where(w => request.ShopNames.Contains(w.FShopName));
            }
        }

        var waybills = await waybillQuery.ToListAsync();
        if (waybills.Count == 0)
            return new BillingRetryResultDto { Message = "未找到匹配的异常运单" };

        var waybillIds = waybills.Select(w => w.FID).ToList();
        var waybillNos = waybills.Select(w => w.FWaybillNo).Distinct().ToList();

        // Step 2: 删除已有的异常计费结果
        var errorResults = await _resultRepo.Query()
            .Where(r => waybillNos.Contains(r.FWaybillNo!) && r.FCalcStatus == 2)
            .ToListAsync();

        // 删除关联的成本明细
        var errorResultIds = errorResults.Select(r => r.FID).ToList();
        if (errorResultIds.Count > 0)
        {
            var costBreakdowns = await _costBreakdownRepo.Query()
                .Where(c => errorResultIds.Contains(c.FBillingResultId))
                .ToListAsync();
            foreach (var cb in costBreakdowns)
                await _costBreakdownRepo.DeleteAsync(cb.FID);
        }

        foreach (var er in errorResults)
            await _resultRepo.DeleteAsync(er.FID);

        // Step 3: 重置运单计费状态
        foreach (var w in waybills)
        {
            w.FBillingStatus = 0;
            w.FBusinessObjectId = null; // 清除之前可能错误的关联
            w.FNetworkPointId = null;
            await _waybillRepo.UpdateAsync(w);
        }

        // Step 4: 重算需要通过 Pipeline 重新触发 PricingPlugin
        // 新的 BillingEngine 需要 sourceTable + batchId，重试场景下
        // 数据已在 EXP出港运单 中而非 STG 暂存表，暂不直接调用引擎
        // TODO: 后续实现方案：
        //   1. 将重置的运单转换为 BillingWaybillData 列表
        //   2. 确定 sourceTable 和 batchId（从原始导入记录反查）
        //   3. 调用 _engine.ExecuteAsync(waybillDataList, sourceTable, batchId)

        return new BillingRetryResultDto
        {
            ResetCount = waybills.Count,
            SuccessCount = 0,
            FailureCount = 0,
            Message = $"已重置 {waybills.Count} 条运单的计费状态，需通过 Pipeline 重新触发 PricingPlugin 完成重算"
        };
    }

    private async Task<BillingResultDto> MapToDto(ExpBillingResult result)
    {
        var waybill = !string.IsNullOrEmpty(result.FWaybillNo)
            ? await _waybillRepo.Query().FirstOrDefaultAsync(w => w.FWaybillNo == result.FWaybillNo)
            : null;
        var quotation = result.FQuotationId.HasValue
            ? await _quotationRepo.GetByIdAsync(result.FQuotationId.Value)
            : null;

        var costBreakdowns = await _costBreakdownRepo.Query()
            .Where(c => c.FBillingResultId == result.FID)
            .ToListAsync();

        // 成本明细的 FCostItemId 与成本矩阵同一 ID 空间（方案成本项 FID），名称从方案成本项表解析；
        // 兼容历史明细中可能残留的全局成本项目 ID，方案项查不到时回退全局表
        var costItemIds = costBreakdowns.Select(c => c.FCostItemId).Distinct().ToList();
        var costItemMap = new Dictionary<int, string>();
        if (costItemIds.Count > 0)
        {
            var planItemIds = costItemIds.Select(i => (long)i).ToList();
            var planItems = await _costPlanItemRepo.Query()
                .Where(i => planItemIds.Contains(i.FID))
                .Select(i => new { i.FID, i.FItemName })
                .ToListAsync();
            foreach (var p in planItems)
                costItemMap[(int)p.FID] = p.FItemName;

            var missingIds = costItemIds.Where(id => !costItemMap.ContainsKey(id)).ToList();
            if (missingIds.Count > 0)
            {
                var globalItems = await _costItemRepo.Query()
                    .Where(i => missingIds.Contains(i.FID))
                    .ToListAsync();
                foreach (var g in globalItems)
                    costItemMap[g.FID] = g.FName;
            }
        }

        return new BillingResultDto
        {
            Id = result.FID,
            WaybillNo = result.FWaybillNo,
            WaybillDate = result.FWaybillDate,
            PartyClientId = result.FPartyClientId,
            PartyClientName = quotation?.FPlanName,
            PartyRole = result.FPartyRole,
            ChainLevel = result.FChainLevel,
            BrandCode = result.FBrandCode,
            BillingDate = result.FBillingDate,
            BillableWeight = result.FBillableWeight,
            FreightCharge = result.FFreightCharge,
            InsuranceFee = result.FInsuranceFee,
            SurchargeAmount = result.FSurchargeAmount,
            WaiverAmount = result.FWaiverAmount,
            CommissionAmount = result.FCommissionAmount,
            ChargeAmount = result.FChargeAmount,
            QuotationId = result.FQuotationId,
            CommissionRuleId = result.FCommissionRuleId,
            CalcStatus = result.FCalcStatus,
            ErrorMessage = result.FErrorMessage,
            InvoiceId = result.FInvoiceId,
            CostBreakdowns = costBreakdowns.Select(c => new BillingCostBreakdownDto
            {
                Id = c.FID,
                CostItemId = c.FCostItemId,
                CostItemName = costItemMap.GetValueOrDefault(c.FCostItemId),
                Amount = c.FAmount
            }).ToList()
        };
    }
}
