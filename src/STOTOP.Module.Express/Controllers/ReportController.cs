using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 统计报表
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/report")]
public class ReportController : ControllerBase
{
    private readonly IFlowAnalysisService _flowService;
    private readonly IWeightSegmentReportService _weightService;
    private readonly IProfitAnalysisService _profitService;
    private readonly IDashboardService _dashboardService;

    public ReportController(
        IFlowAnalysisService flowService,
        IWeightSegmentReportService weightService,
        IProfitAnalysisService profitService,
        IDashboardService dashboardService)
    {
        _flowService = flowService;
        _weightService = weightService;
        _profitService = profitService;
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// 流量分布
    /// </summary>
    [HttpGet("flow-distribution")]
    [RequirePermission(ExpressPermissions.ReportView)]
    public async Task<ApiResult<List<FlowAnalysisDto>>> GetFlowDistribution([FromQuery] ReportQueryRequest request)
    {
        var result = await _flowService.GetFlowDistributionAsync(request);
        return ApiResult<List<FlowAnalysisDto>>.Success(result);
    }

    /// <summary>
    /// 流量趋势
    /// </summary>
    [HttpGet("flow-trend")]
    [RequirePermission(ExpressPermissions.ReportView)]
    public async Task<ApiResult<List<FlowTrendDto>>> GetFlowTrend([FromQuery] ReportQueryRequest request, [FromQuery] string granularity = "day")
    {
        var result = await _flowService.GetFlowTrendAsync(request, granularity);
        return ApiResult<List<FlowTrendDto>>.Success(result);
    }

    /// <summary>
    /// 重量段分布
    /// </summary>
    [HttpGet("weight-distribution")]
    [RequirePermission(ExpressPermissions.ReportView)]
    public async Task<ApiResult<List<WeightSegmentReportDto>>> GetWeightDistribution([FromQuery] ReportQueryRequest request)
    {
        var result = await _weightService.GetWeightDistributionAsync(request);
        return ApiResult<List<WeightSegmentReportDto>>.Success(result);
    }

    /// <summary>
    /// 均重趋势
    /// </summary>
    [HttpGet("weight-trend")]
    [RequirePermission(ExpressPermissions.ReportView)]
    public async Task<ApiResult<List<WeightTrendDto>>> GetWeightTrend([FromQuery] ReportQueryRequest request, [FromQuery] string granularity = "day")
    {
        var result = await _weightService.GetAvgWeightTrendAsync(request, granularity);
        return ApiResult<List<WeightTrendDto>>.Success(result);
    }

    /// <summary>
    /// 按客户毛利
    /// </summary>
    [HttpGet("profit-by-client")]
    [RequirePermission(ExpressPermissions.ReportView)]
    public async Task<ApiResult<List<ProfitByClientDto>>> GetProfitByClient([FromQuery] ReportQueryRequest request)
    {
        var result = await _profitService.GetProfitByClientAsync(request);
        return ApiResult<List<ProfitByClientDto>>.Success(result);
    }

    /// <summary>
    /// 按店铺毛利
    /// </summary>
    [HttpGet("profit-by-shop")]
    [RequirePermission(ExpressPermissions.ReportView)]
    public async Task<ApiResult<List<ProfitByShopDto>>> GetProfitByShop([FromQuery] ReportQueryRequest request)
    {
        var result = await _profitService.GetProfitByShopAsync(request);
        return ApiResult<List<ProfitByShopDto>>.Success(result);
    }

    /// <summary>
    /// 毛利趋势
    /// </summary>
    [HttpGet("profit-trend")]
    [RequirePermission(ExpressPermissions.ReportView)]
    public async Task<ApiResult<List<ProfitTrendDto>>> GetProfitTrend([FromQuery] ReportQueryRequest request, [FromQuery] string granularity = "month")
    {
        var result = await _profitService.GetProfitTrendAsync(request, granularity);
        return ApiResult<List<ProfitTrendDto>>.Success(result);
    }

    /// <summary>
    /// 中间人视角毛利
    /// </summary>
    [HttpGet("profit-by-intermediary")]
    [RequirePermission(ExpressPermissions.ReportView)]
    public async Task<ApiResult<List<ProfitByIntermediaryDto>>> GetProfitByIntermediary([FromQuery] ReportQueryRequest request)
    {
        var result = await _profitService.GetProfitByIntermediaryAsync(request);
        return ApiResult<List<ProfitByIntermediaryDto>>.Success(result);
    }

    /// <summary>
    /// 业务员提成视角毛利
    /// </summary>
    [HttpGet("profit-by-salesman")]
    [RequirePermission(ExpressPermissions.ReportView)]
    public async Task<ApiResult<List<ProfitBySalesmanDto>>> GetProfitBySalesman([FromQuery] ReportQueryRequest request)
    {
        var result = await _profitService.GetProfitBySalesmanAsync(request);
        return ApiResult<List<ProfitBySalesmanDto>>.Success(result);
    }

    /// <summary>
    /// 按重量段毛利
    /// </summary>
    [HttpGet("profit-by-weight-segment")]
    [RequirePermission(ExpressPermissions.ReportView)]
    public async Task<ApiResult<List<ProfitByWeightSegmentDto>>> GetProfitByWeightSegment([FromQuery] ReportQueryRequest request)
    {
        var data = await _profitService.GetProfitByWeightSegmentAsync(request);
        return ApiResult<List<ProfitByWeightSegmentDto>>.Success(data);
    }

    /// <summary>
    /// 按大区流量损益
    /// </summary>
    [HttpGet("profit-by-region")]
    [RequirePermission(ExpressPermissions.ReportView)]
    public async Task<ApiResult<List<ProfitByRegionDto>>> GetProfitByRegion([FromQuery] ReportQueryRequest request)
    {
        var result = await _profitService.GetProfitByRegionAsync(request);
        return ApiResult<List<ProfitByRegionDto>>.Success(result);
    }

    /// <summary>
    /// 按省份流量损益
    /// </summary>
    [HttpGet("profit-by-province")]
    [RequirePermission(ExpressPermissions.ReportView)]
    public async Task<ApiResult<List<ProfitByProvinceDto>>> GetProfitByProvince([FromQuery] ReportQueryRequest request, [FromQuery] string? region)
    {
        var result = await _profitService.GetProfitByProvinceAsync(request, region);
        return ApiResult<List<ProfitByProvinceDto>>.Success(result);
    }

    /// <summary>
    /// 筛选下拉选项
    /// </summary>
    [HttpGet("filter-options")]
    [RequirePermission(ExpressPermissions.ReportView)]
    public async Task<ApiResult<ProfitFilterOptionsDto>> GetFilterOptions()
    {
        var result = await _profitService.GetFilterOptionsAsync();
        return ApiResult<ProfitFilterOptionsDto>.Success(result);
    }

    /// <summary>
    /// 综合看板
    /// </summary>
    [HttpGet("dashboard")]
    [RequirePermission(ExpressPermissions.DashboardView)]
    public async Task<ApiResult<DashboardDto>> GetDashboard([FromQuery] string? brandCode)
    {
        var result = await _dashboardService.GetDashboardAsync(brandCode);
        return ApiResult<DashboardDto>.Success(result);
    }
}
