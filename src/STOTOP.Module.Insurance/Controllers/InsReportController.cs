using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Controllers;

/// <summary>
/// 保险报表控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/insurance/reports")]
public class InsReportController : ControllerBase
{
    private readonly IReportService _service;

    public InsReportController(IReportService service)
    {
        _service = service;
    }

    /// <summary>
    /// 保费汇总报表
    /// </summary>
    [HttpGet("premium-summary")]
    [RequirePermission(InsurancePermissions.ReportView)]
    public async Task<ApiResult<List<PremiumSummaryItem>>> GetPremiumSummary(
        [FromQuery] long orgId,
        [FromQuery] int? businessType = null,
        [FromQuery] int? insuranceCategory = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var result = await _service.GetPremiumSummaryAsync(orgId, businessType, insuranceCategory, startDate, endDate);
        return ApiResult<List<PremiumSummaryItem>>.Success(result);
    }

    /// <summary>
    /// 出险分析报表
    /// </summary>
    [HttpGet("claim-analysis")]
    [RequirePermission(InsurancePermissions.ReportView)]
    public async Task<ApiResult<List<ClaimAnalysisItem>>> GetClaimAnalysis(
        [FromQuery] long orgId,
        [FromQuery] int? accidentType = null,
        [FromQuery] int? businessType = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var result = await _service.GetClaimAnalysisAsync(orgId, accidentType, businessType, startDate, endDate);
        return ApiResult<List<ClaimAnalysisItem>>.Success(result);
    }

    /// <summary>
    /// 赔付分析报表
    /// </summary>
    [HttpGet("settlement-analysis")]
    [RequirePermission(InsurancePermissions.ReportView)]
    public async Task<ApiResult<List<SettlementAnalysisItem>>> GetSettlementAnalysis(
        [FromQuery] long orgId,
        [FromQuery] int? settlementType = null,
        [FromQuery] int? settlementStatus = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var result = await _service.GetSettlementAnalysisAsync(orgId, settlementType, settlementStatus, startDate, endDate);
        return ApiResult<List<SettlementAnalysisItem>>.Success(result);
    }

    /// <summary>
    /// 基金收支报表
    /// </summary>
    [HttpGet("fund-balance")]
    [RequirePermission(InsurancePermissions.ReportView)]
    public async Task<ApiResult<List<FundBalanceItem>>> GetFundBalance(
        [FromQuery] long orgId,
        [FromQuery] long? fundId = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var result = await _service.GetFundBalanceAsync(orgId, fundId, startDate, endDate);
        return ApiResult<List<FundBalanceItem>>.Success(result);
    }

    /// <summary>
    /// 综合看板
    /// </summary>
    [HttpGet("overview")]
    [RequirePermission(InsurancePermissions.ReportView)]
    public async Task<ApiResult<InsuranceOverviewDto>> GetOverview([FromQuery] long orgId)
    {
        var result = await _service.GetOverviewAsync(orgId);
        return ApiResult<InsuranceOverviewDto>.Success(result);
    }
}
