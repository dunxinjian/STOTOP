using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Quality.Dtos.CarrierDashboard;
using STOTOP.Module.Quality.Services.CarrierDashboard;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Quality.Controllers;

[Authorize]
[ApiController]
[Route("api/quality/carrier-dashboard")]
public class CarrierQualityDashboardController : ControllerBase
{
    private readonly ICarrierQualityDashboardService _service;

    public CarrierQualityDashboardController(ICarrierQualityDashboardService service)
    {
        _service = service;
    }

    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    // ── 视图1 网点总览 ──
    [HttpGet("network/kpi")]
    [RequirePermission(QualityPermissions.CarrierQualityView)]
    public Task<ApiResult<NetworkKpiDto>> GetNetworkKpi([FromQuery] string carrier, [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? networkCode)
        => _service.GetNetworkKpiAsync(GetOrgId(), carrier, from, to, networkCode);

    [HttpGet("network/trend")]
    [RequirePermission(QualityPermissions.CarrierQualityView)]
    public Task<ApiResult<List<NetworkTrendPointDto>>> GetNetworkTrend([FromQuery] string carrier, [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? networkCode)
        => _service.GetNetworkTrendAsync(GetOrgId(), carrier, from, to, networkCode);

    [HttpGet("network/domain-distribution")]
    [RequirePermission(QualityPermissions.CarrierQualityView)]
    public Task<ApiResult<List<DomainStatItem>>> GetDomainDistribution([FromQuery] string carrier, [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? networkCode)
        => _service.GetDomainDistributionAsync(GetOrgId(), carrier, from, to, networkCode);

    [HttpGet("network/fee-by-domain")]
    [RequirePermission(QualityPermissions.CarrierQualityView)]
    public Task<ApiResult<List<DomainStatItem>>> GetFeeByDomain([FromQuery] string carrier, [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? networkCode)
        => _service.GetFeeByDomainAsync(GetOrgId(), carrier, from, to, networkCode);

    [HttpGet("network/options")]
    [RequirePermission(QualityPermissions.CarrierQualityView)]
    public Task<ApiResult<List<NetworkOptionDto>>> GetNetworkOptions([FromQuery] string carrier)
        => _service.GetNetworkOptionsAsync(GetOrgId(), carrier);

    // ── 视图2 员工质量 ──
    [HttpGet("employee/rank")]
    [RequirePermission(QualityPermissions.CarrierQualityView)]
    public Task<ApiResult<EmployeeRankDto>> GetEmployeeRank([FromQuery] string carrier, [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? networkCode, [FromQuery] string dimension = "problem", [FromQuery] int topN = 10)
        => _service.GetEmployeeRankAsync(GetOrgId(), carrier, from, to, networkCode, dimension, topN);

    [HttpGet("employee/metrics")]
    [RequirePermission(QualityPermissions.CarrierQualityView)]
    public Task<ApiResult<EmployeeMetricsPageDto>> GetEmployeeMetrics([FromQuery] string carrier, [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? networkCode, [FromQuery] int page = 1, [FromQuery] int size = 50)
        => _service.GetEmployeeMetricsAsync(GetOrgId(), carrier, from, to, networkCode, page, size);

    [HttpGet("employee/{empNo}/timeline")]
    [RequirePermission(QualityPermissions.CarrierQualityView)]
    public Task<ApiResult<List<EmployeeEventItemDto>>> GetEmployeeTimeline([FromRoute] string empNo, [FromQuery] string carrier, [FromQuery] DateTime from, [FromQuery] DateTime to)
        => _service.GetEmployeeTimelineAsync(GetOrgId(), carrier, empNo, from, to);

    // ── 视图3 问题件追踪 ──
    [HttpGet("events")]
    [RequirePermission(QualityPermissions.CarrierQualityView)]
    public Task<ApiResult<EventPageDto>> GetEvents([FromQuery] EventQuery query)
        => _service.GetEventsAsync(GetOrgId(), query);

    [HttpGet("pending-count")]
    [RequirePermission(QualityPermissions.CarrierQualityView)]
    public Task<ApiResult<int>> GetPendingCount([FromQuery] string carrier)
        => _service.GetPendingCountAsync(GetOrgId(), carrier);

    [HttpGet("events/export")]
    [RequirePermission(QualityPermissions.CarrierQualityView)]
    public async Task<IActionResult> ExportEvents([FromQuery] EventQuery query)
    {
        query.Page = 1;
        query.Size = 100000; // 一次性全量（按期上千行，足够）
        var result = await _service.GetEventsAsync(GetOrgId(), query);
        var rows = result.Data?.Items ?? new List<QualityEventRowDto>();

        string Esc(string? s)
        {
            s ??= "";
            return s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        }

        var sb = new global::System.Text.StringBuilder();
        sb.AppendLine("业务日期,运单号,网点编码,网点名称,员工工号,员工姓名,质量域,问题类型,严重度,是否考核,考核金额,平台,待认领,多域命中");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                Esc(r.Date?.ToString("yyyy-MM-dd")), Esc(r.Waybill), Esc(r.NetworkCode), Esc(r.NetworkName),
                Esc(r.EmpNo), Esc(r.EmpNameRaw), Esc(r.Domain), Esc(r.ProblemName), r.Severity.ToString(),
                r.IsAssessed ? "是" : "否", (r.Fee ?? 0m).ToString("0.00"), Esc(r.Platform),
                r.IsPending ? "是" : "否", r.IsMultiDomain ? "是" : "否"
            }));
        }

        var body = global::System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(body).ToArray();
        var fileName = $"问题件追踪-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        return File(bytes, "text/csv;charset=utf-8", fileName);
    }
}
