using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using STOTOP.WebAPI.Services;

namespace STOTOP.WebAPI.Jobs;

/// <summary>
/// 每周经营周报机器人推送 — 周一 09:00 触发
/// </summary>
[AutomaticRetry(Attempts = 2)]
public class WeeklyReportBotJob
{
    private readonly DingTalkBotService _botService;
    private readonly IConfiguration _config;
    private readonly ILogger<WeeklyReportBotJob> _logger;

    public WeeklyReportBotJob(
        DingTalkBotService botService,
        IConfiguration config,
        ILogger<WeeklyReportBotJob> logger)
    {
        _botService = botService;
        _config = config;
        _logger = logger;
    }

    public async Task Execute()
    {
        _logger.LogInformation("[WeeklyReportBot] 开始生成每周报...");

        try
        {
            var today = DateTime.Today;
            // 上周一 ~ 上周日
            int diffToMonday = ((int)today.DayOfWeek + 6) % 7; // Mon=0
            var thisMonday = today.AddDays(-diffToMonday);
            var lastMonday = thisMonday.AddDays(-7);
            var lastSunday = thisMonday.AddDays(-1);

            // TODO: 聚合本周（上周）经营概况 vs 上上周对比
            var wVolume = 4120;
            var wVolumeChangePct = 6.5;
            var wRevenue = 86430m;
            var wRevenueChangePct = 4.1;
            var wCost = 73200m;
            var wCostChangePct = -2.0;
            var wProfit = 13230m;
            var wProfitChangePct = 18.7;

            string title = $"STOTOP 经营周报 {lastMonday:MM-dd} ~ {lastSunday:MM-dd}";
            string text = string.Join("\n",
                "## 经营周报",
                "",
                $"**周期:** {lastMonday:yyyy-MM-dd} ~ {lastSunday:yyyy-MM-dd}",
                "",
                $"**本周票量:** {wVolume:N0}票 ({SignPct(wVolumeChangePct)} {ArrowD(wVolumeChangePct)})",
                $"**本周收入:** ￥{wRevenue:N0} ({SignPct(wRevenueChangePct)} {ArrowD(wRevenueChangePct)})",
                $"**本周成本:** ￥{wCost:N0} ({SignPct(wCostChangePct)} {ArrowD(-wCostChangePct)})",
                $"**本周利润:** ￥{wProfit:N0} ({SignPct(wProfitChangePct)} {ArrowD(wProfitChangePct)})",
                "",
                "---",
                "",
                "环比上周对比已包含在百分比中"
            );

            var domain = _config["DingTalk:RedirectDomain"] ?? "http://localhost:9000";
            var linkUrl = $"{domain}/redirect/dashboard";

            var result = await _botService.SendActionCard(title, text, "查看详情", linkUrl);
            if (result.Success)
            {
                _logger.LogInformation("[WeeklyReportBot] 每周报推送成功");
            }
            else
            {
                _logger.LogWarning("[WeeklyReportBot] 每周报推送失败: {Msg}", result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WeeklyReportBot] 执行失败");
            throw;
        }
    }

    private static string SignPct(double v) => (v >= 0 ? "+" : "") + v.ToString("F1") + "%";
    private static string ArrowD(double v) => v >= 0 ? "↑" : "↓";
}
