using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace STOTOP.WebAPI.Controllers.Mobile;

/// <summary>
/// 移动端经营看板聚合接�?
/// </summary>
[ApiController]
[Route("api/mobile/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ILogger<DashboardController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// KPI 汇总数�?
    /// </summary>
    /// <param name="period">期间，格�?yyyy-MM</param>
    /// <param name="orgId">组织 ID</param>
    [HttpGet("kpi")]
    public async Task<IActionResult> GetKpi([FromQuery] string period, [FromQuery] int orgId)
    {
        _logger.LogInformation("[Dashboard] GetKpi period={Period}, orgId={OrgId}", period, orgId);

        // TODO: 从缓存或数据库获�?KPI 数据，后续接�?DashboardCacheJob 预计�?
        await Task.CompletedTask;

        return Ok(new
        {
            code = 200,
            data = new
            {
                volume = new { value = 12580, change = 5.2 },
                revenue = new { value = 328500, change = 3.1 },
                cost = new { value = 285200, change = -1.8 },
                profit = new { value = 43300, change = 12.5 },
                cachedAt = DateTime.UtcNow.ToString("o")
            },
            message = "ok"
        });
    }

    /// <summary>
    /// 趋势数据
    /// </summary>
    /// <param name="days">天数，默�?0</param>
    /// <param name="metric">指标�?revenue/volume/cost/profit</param>
    /// <param name="orgId">组织 ID</param>
    [HttpGet("trend")]
    public async Task<IActionResult> GetTrend(
        [FromQuery] int days = 30,
        [FromQuery] string metric = "revenue",
        [FromQuery] int orgId = 0)
    {
        _logger.LogInformation("[Dashboard] GetTrend days={Days}, metric={Metric}, orgId={OrgId}",
            days, metric, orgId);

        // TODO: 从缓存获取趋势数�?
        await Task.CompletedTask;

        var random = new Random(42); // 固定种子确保同请求稳�?
        var points = Enumerable.Range(0, days).Select(i => new
        {
            date = DateTime.Today.AddDays(-days + i + 1).ToString("yyyy-MM-dd"),
            value = random.Next(8000, 15000)
        }).ToList();

        return Ok(new
        {
            code = 200,
            data = new { points },
            message = "ok"
        });
    }
}
