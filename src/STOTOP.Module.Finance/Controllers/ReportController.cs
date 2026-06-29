using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/reports")]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly AmoebaPLService _amoebaPLService;
    private readonly STOTOPDbContext _dbContext;

    public ReportController(IReportService reportService, AmoebaPLService amoebaPLService, STOTOPDbContext dbContext)
    {
        _reportService = reportService;
        _amoebaPLService = amoebaPLService;
        _dbContext = dbContext;
    }

    [HttpGet("account-balance")]
    public async Task<ApiResult<List<AccountBalanceDto>>> GetAccountBalance(
        [FromQuery] long periodId, 
        [FromQuery] long? accountId,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetAccountBalanceAsync(periodId, accountId, accountSetId);
        return ApiResult<List<AccountBalanceDto>>.Success(result);
    }

    /// <summary>
    /// 从凭证计算科目余额表（按年月查询）
    /// </summary>
    [HttpGet("account-balance/calculate")]
    public async Task<ApiResult<List<AccountBalanceDto>>> GetAccountBalanceByYearMonth(
        [FromQuery] int year, 
        [FromQuery] int month,
        [FromQuery] long? accountId = null,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetAccountBalanceByYearMonthAsync(year, month, accountId, accountSetId);
        return ApiResult<List<AccountBalanceDto>>.Success(result);
    }

    [HttpGet("auxiliary-balance")]
    public async Task<ApiResult<List<AuxiliaryBalanceDto>>> GetAuxiliaryBalance(
        [FromQuery] long periodId, 
        [FromQuery] string? type,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetAuxiliaryBalanceAsync(periodId, type, accountSetId);
        return ApiResult<List<AuxiliaryBalanceDto>>.Success(result);
    }

    [HttpGet("asset-balance")]
    public async Task<ApiResult<List<AssetBalanceDto>>> GetAssetBalance([FromQuery] long? periodId, [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetAssetBalanceAsync(periodId, accountSetId);
        return ApiResult<List<AssetBalanceDto>>.Success(result);
    }

    [HttpGet("profit-statement")]
    public async Task<ApiResult<List<ProfitStatementDto>>> GetProfitStatement(
        [FromQuery] long startPeriodId, 
        [FromQuery] long endPeriodId, 
        [FromQuery] string format = "small",
        [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetProfitStatementAsync(startPeriodId, endPeriodId, format, accountSetId);
        return ApiResult<List<ProfitStatementDto>>.Success(result);
    }

    /// <summary>
    /// 小企业利润表（按年月查询）
    /// </summary>
    [HttpGet("small-enterprise-profit-statement")]
    public async Task<ApiResult<List<SmallEnterpriseProfitStatementDto>>> GetSmallEnterpriseProfitStatement(
        [FromQuery] int year, 
        [FromQuery] int month,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetSmallEnterpriseProfitStatementAsync(year, month, accountSetId);
        return ApiResult<List<SmallEnterpriseProfitStatementDto>>.Success(result);
    }

    [HttpGet("balance-sheet")]
    public async Task<ApiResult<List<BalanceSheetDto>>> GetBalanceSheet([FromQuery] long periodId, [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetBalanceSheetAsync(periodId, accountSetId);
        return ApiResult<List<BalanceSheetDto>>.Success(result);
    }

    [HttpGet("cash-flow-report")]
    public async Task<ApiResult<List<CashFlowDto>>> GetCashFlowReport(
        [FromQuery] long startPeriodId, 
        [FromQuery] long endPeriodId,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetCashFlowAsync(startPeriodId, endPeriodId, accountSetId);
        return ApiResult<List<CashFlowDto>>.Success(result);
    }

    [HttpGet("tax-payable")]
    public async Task<ApiResult<List<TaxPayableDto>>> GetTaxPayable([FromQuery] long periodId, [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetTaxPayableAsync(periodId, accountSetId);
        return ApiResult<List<TaxPayableDto>>.Success(result);
    }

    [HttpGet("amoeba-pl")]
    public async Task<ApiResult<AmoebaPLReportDto>> GetAmoebaPL(
        [FromQuery] long startPeriodId, 
        [FromQuery] long endPeriodId, 
        [FromQuery] long? departmentId, 
        [FromQuery] long? amoebaId,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetAmoebaPLAsync(startPeriodId, endPeriodId, departmentId, amoebaId, accountSetId);
        return ApiResult<AmoebaPLReportDto>.Success(result);
    }

    /// <summary>
    /// 查询科目明细账
    /// </summary>
    [HttpGet("account-detail")]
    public async Task<ApiResult<AccountDetailResultDto>> GetAccountDetail(
        [FromQuery] long accountId,
        [FromQuery] int year,
        [FromQuery] int periodNo,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetAccountDetailAsync(accountId, year, periodNo, accountSetId);
        return ApiResult<AccountDetailResultDto>.Success(result);
    }

    [HttpPost("recalculate/{periodId}")]
    public async Task<ApiResult> RecalculateBalance(long periodId, [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.RecalculateBalanceAsync(periodId, accountSetId);
        if (!result)
        {
            return ApiResult.Fail("重算余额失败");
        }
        return ApiResult.Ok("重算余额成功");
    }

    /// <summary>
    /// 报表钻取明细
    /// </summary>
    [HttpGet("drill-down")]
    public async Task<ApiResult<List<DrillDownItemDto>>> GetDrillDown(
        [FromQuery] string reportType,
        [FromQuery] int rowIndex,
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] long accountSetId = 0,
        [FromQuery] string? accountCode = null)
    {
        var result = await _reportService.GetDrillDownAsync(reportType, rowIndex, year, month, accountSetId, accountCode);
        return ApiResult<List<DrillDownItemDto>>.Success(result);
    }

    /// <summary>
    /// 利润12月趋势数据
    /// </summary>
    [HttpGet("profit-trend")]
    public async Task<ApiResult<List<ProfitTrendDto>>> GetProfitTrend(
        [FromQuery] int year,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetProfitTrendAsync(year, accountSetId);
        return ApiResult<List<ProfitTrendDto>>.Success(result);
    }

    /// <summary>
    /// 收入构成数据
    /// </summary>
    [HttpGet("revenue-composition")]
    public async Task<ApiResult<List<CompositionItemDto>>> GetRevenueComposition(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetRevenueCompositionAsync(year, month, accountSetId);
        return ApiResult<List<CompositionItemDto>>.Success(result);
    }

    /// <summary>
    /// 费用构成数据
    /// </summary>
    [HttpGet("expense-composition")]
    public async Task<ApiResult<List<CompositionItemDto>>> GetExpenseComposition(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetExpenseCompositionAsync(year, month, accountSetId);
        return ApiResult<List<CompositionItemDto>>.Success(result);
    }

    /// <summary>
    /// 同比对比数据
    /// </summary>
    [HttpGet("yoy-comparison")]
    public async Task<ApiResult<List<ComparisonDto>>> GetYoYComparison(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetYoYComparisonAsync(year, month, accountSetId);
        return ApiResult<List<ComparisonDto>>.Success(result);
    }

    /// <summary>
    /// 环比对比数据
    /// </summary>
    [HttpGet("mom-comparison")]
    public async Task<ApiResult<List<ComparisonDto>>> GetMoMComparison(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _reportService.GetMoMComparisonAsync(year, month, accountSetId);
        return ApiResult<List<ComparisonDto>>.Success(result);
    }

    /// <summary>
    /// 新版阿米巴经营报表
    /// </summary>
    [HttpPost("amoeba-report")]
    public async Task<ApiResult<AmoebaReportResponse>> GetAmoebaReport([FromBody] AmoebaReportRequest request)
    {
        var result = await _amoebaPLService.GetReportAsync(request);
        return ApiResult<AmoebaReportResponse>.Success(result);
    }

    /// <summary>
    /// 多期对比阿米巴经营报表（功能分区制）
    /// </summary>
    [HttpPost("amoeba-report/multi-period")]
    public async Task<ApiResult<AmoebaMultiPeriodResponse>> GetMultiPeriodReport([FromBody] AmoebaMultiPeriodRequest request)
    {
        try
        {
            var result = await _amoebaPLService.GetMultiPeriodReportAsync(request);
            return ApiResult<AmoebaMultiPeriodResponse>.Success(result);
        }
        catch (ArgumentException ex)
        {
            return ApiResult<AmoebaMultiPeriodResponse>.Fail(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AmoebaMultiPeriodResponse>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 阿米巴钻取明细
    /// </summary>
    [HttpGet("amoeba-report/drill-down")]
    public async Task<ApiResult<AmoebaDrillDownResponse>> GetAmoebaDrillDown(
        [FromQuery] long unitId, [FromQuery] DateTime date, [FromQuery] string category, [FromQuery] long accountSetId = 0)
    {
        var result = await _amoebaPLService.GetDrillDownAsync(unitId, date, category, accountSetId);
        return ApiResult<AmoebaDrillDownResponse>.Success(result);
    }

    /// <summary>
    /// 损益项明细钻取（科目汇总→凭证分录）
    /// </summary>
    [HttpPost("amoeba-report/pl-item-detail")]
    public async Task<ApiResult<AmoebaPLItemDetailResponse>> GetPLItemDetail([FromBody] AmoebaPLItemDetailRequest request)
    {
        var result = await _amoebaPLService.GetPLItemDetailAsync(request);
        return ApiResult<AmoebaPLItemDetailResponse>.Success(result);
    }

    /// <summary>
    /// 出港收入按业务对象下钻（当损益项为出港收入时使用）
    /// </summary>
    [HttpPost("amoeba-report/billing-drill-down")]
    public async Task<ApiResult<AmoebaBillingDrillDownResponse>> GetBillingDrillDown([FromBody] AmoebaPLItemDetailRequest request)
    {
        var result = await _amoebaPLService.GetBillingDrillDownAsync(request);
        return ApiResult<AmoebaBillingDrillDownResponse>.Success(result);
    }

    /// <summary>
    /// 判断损益项是否为出港收入类型（前端路由判断用）
    /// </summary>
    [HttpGet("amoeba-report/is-outbound-revenue/{plItemId}")]
    public async Task<ApiResult<bool>> IsOutboundRevenueItem(long plItemId)
    {
        var result = await _amoebaPLService.IsOutboundRevenueItemAsync(plItemId);
        return ApiResult<bool>.Success(result);
    }

    /// <summary>
    /// 判断损益项是否为折旧数据源类型（前端路由判断用）
    /// </summary>
    [HttpGet("amoeba-report/is-depreciation/{plItemId}")]
    public async Task<ApiResult<bool>> IsDepreciationItem(long plItemId)
    {
        var result = await _amoebaPLService.IsDepreciationItemAsync(plItemId);
        return ApiResult<bool>.Success(result);
    }

    /// <summary>
    /// 折旧项下钻：返回资产卡片折旧明细
    /// </summary>
    [HttpPost("amoeba-report/depreciation-drill-down")]
    public async Task<ApiResult<DepreciationDrillDownResponse>> GetDepreciationDrillDown([FromBody] DepreciationDrillDownRequest request)
    {
        var result = await _amoebaPLService.GetDepreciationDrillDownAsync(
            request.PlItemId, request.StartDate, request.EndDate, request.AccountSetId);
        return ApiResult<DepreciationDrillDownResponse>.Success(result);
    }

    /// <summary>
    /// 导出阿米巴经营报表为Excel
    /// </summary>
    [HttpPost("amoeba-report/export")]
    public async Task<IActionResult> ExportAmoebaReport([FromBody] AmoebaReportRequest request, [FromQuery] long accountSetId)
    {
        var bytes = await _amoebaPLService.ExportReportToExcelAsync(request, accountSetId);
        var fileName = $"阿米巴经营报表_{DateTime.Now:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>获取手工填报数据</summary>
    [HttpGet("amoeba-report/manual-data")]
    public async Task<ApiResult<List<ManualDataDto>>> GetManualData(
        [FromQuery] long templateId, [FromQuery] long orgId, [FromQuery] string period)
    {
        var effectiveOrgId = GetCurrentOrgId();
        if (effectiveOrgId <= 0) effectiveOrgId = orgId;

        var list = await _dbContext.Set<FinAmoebaManualData>()
            .Where(x => x.FTemplateId == templateId
                && x.FOrgId == effectiveOrgId
                && x.FPeriod == period
                && x.FDataType == "manual")
            .Select(x => new ManualDataDto
            {
                Id = x.FID,
                PLItemId = x.FPLItemId,
                Amount = x.FAmount,
                PerUnitValue = x.FPerUnitValue,
            })
            .ToListAsync();
        // 统一 ApiResult 信封：前端拦截器按 {code,data} 解包，裸数组会被解成 undefined
        return ApiResult<List<ManualDataDto>>.Success(list);
    }

    /// <summary>批量保存手工填报数据（UPSERT）</summary>
    [HttpPost("amoeba-report/manual-data")]
    public async Task<ApiResult> SaveManualData([FromBody] SaveManualDataRequest request)
    {
        var effectiveOrgId = GetCurrentOrgId();
        if (effectiveOrgId <= 0) effectiveOrgId = request.OrgId;
        if (request.Items.Count == 0) return ApiResult.Ok("保存成功");

        // 校验损益项归属模板，拒绝写入悬空数据
        var plItemIds = request.Items.Select(i => i.PLItemId).Distinct().ToList();
        var validIds = (await _dbContext.Set<FinAmoebaPLItem>()
                .Where(i => i.FTemplateId == request.TemplateId && plItemIds.Contains(i.FID))
                .Select(i => i.FID)
                .ToListAsync())
            .ToHashSet();
        var invalid = plItemIds.Where(id => !validIds.Contains(id)).ToList();
        if (invalid.Count > 0)
        {
            return ApiResult.Fail($"存在不属于该模板的损益项（ID: {string.Join(",", invalid.Take(5))}），请刷新后重试");
        }

        // 一次性取出现有记录，避免逐条查询
        var existingList = await _dbContext.Set<FinAmoebaManualData>()
            .Where(x => x.FTemplateId == request.TemplateId
                && x.FOrgId == effectiveOrgId
                && x.FPeriod == request.Period
                && x.FDataType == "manual"
                && x.FPLItemId != null && plItemIds.Contains(x.FPLItemId.Value))
            .ToListAsync();
        var existingByItem = existingList
            .GroupBy(x => x.FPLItemId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var now = DateTime.Now;
        foreach (var item in request.Items)
        {
            if (existingByItem.TryGetValue(item.PLItemId, out var existing))
            {
                existing.FAmount = item.Amount;
                existing.FPerUnitValue = item.PerUnitValue;
                existing.FPeriodKey = AmoebaPLService.BuildPeriodKey(request.Period);   // [批次5-S3] 自愈期间键(幂等)
                existing.FUpdatedTime = now;
            }
            else
            {
                _dbContext.Set<FinAmoebaManualData>().Add(new FinAmoebaManualData
                {
                    FTemplateId = request.TemplateId,
                    FPLItemId = item.PLItemId,
                    FOrgId = effectiveOrgId,
                    FPeriod = request.Period,
                    FPeriodKey = AmoebaPLService.BuildPeriodKey(request.Period),   // [批次5-S3] 手工填报当前为月度
                    FAmount = item.Amount,
                    FPerUnitValue = item.PerUnitValue,
                    FDataType = "manual",
                    FCreatedTime = now,
                    FUpdatedTime = now,
                });
            }
        }
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // 并发保存撞唯一索引 (FTemplateId,FPLItemId,FOrgId,FPeriod)
            return ApiResult.Fail("保存冲突：该期间数据正被其他人同时保存，请刷新后重试");
        }
        return ApiResult.Ok("保存成功");
    }

    /// <summary>获取暂估数据列表</summary>
    [HttpGet("amoeba-report/estimate-data")]
    public async Task<ApiResult<List<ManualDataDto>>> GetEstimateData(
        [FromQuery] long templateId, [FromQuery] long orgId, [FromQuery] string period)
    {
        var effectiveOrgId = GetCurrentOrgId();
        if (effectiveOrgId <= 0) effectiveOrgId = orgId;

        var list = await _amoebaPLService.GetEstimateDataAsync(templateId, effectiveOrgId, period);
        return ApiResult<List<ManualDataDto>>.Success(list);
    }

    /// <summary>保存暂估数据（UPSERT单条）</summary>
    [HttpPost("amoeba-report/estimate-data")]
    public async Task<ApiResult> SaveEstimateData([FromBody] ManualDataDto dto)
    {
        var effectiveOrgId = GetCurrentOrgId();
        if (effectiveOrgId > 0) dto.OrgId = effectiveOrgId;
        dto.DataType = "estimate";
        await _amoebaPLService.SaveEstimateDataAsync(dto);
        return ApiResult.Ok("保存成功");
    }

    /// <summary>删除暂估数据</summary>
    [HttpDelete("amoeba-report/estimate-data/{id}")]
    public async Task<ApiResult> DeleteEstimateData(long id)
    {
        await _amoebaPLService.DeleteEstimateDataAsync(id, GetCurrentOrgId());
        return ApiResult.Ok("删除成功");
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = HttpContext.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }
}
