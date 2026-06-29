using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using STOTOP.WebAPI.Services;

namespace STOTOP.WebAPI.Jobs;

/// <summary>
/// 每日经营晨报机器人推送 — 工作日 08:00 触发
/// </summary>
[AutomaticRetry(Attempts = 2)]
public class DailyReportBotJob
{
    private readonly DingTalkBotService _botService;
    private readonly IConfiguration _config;
    private readonly ILogger<DailyReportBotJob> _logger;

    public DailyReportBotJob(
        DingTalkBotService botService,
        IConfiguration config,
        ILogger<DailyReportBotJob> logger)
    {
        _botService = botService;
        _config = config;
        _logger = logger;
    }

    public async Task Execute()
    {
        _logger.LogInformation("[DailyReportBot] 开始生成每日晨报...");

        try
        {
            var yesterday = DateTime.Today.AddDays(-1);

            // TODO: 对接业务模块（Express/Finance/Amoeba）聚合昨日票量/收入/成本/利润
            var yVolume = 580;
            var yVolumeChange = 12;
            var yRevenue = 12350m;
            var yRevenueChangePct = 3.2;
            var yCost = 10800m;
            var yCostChangePct = -1.5;
            var yProfit = 1550m;
            var yProfitChangePct = 28.0;

            // TODO: 聚合本月累计数据
            var mVolume = 12580;
            var mRevenue = 328500m;
            var mCost = 285200m;
            var mProfit = 43300m;

            string title = $"STOTOP 经营日报 {yesterday:yyyy-MM-dd}";
            string text = string.Join("\n",
                "## 经营日报",
                "",
                $"**昨日票量:** {yVolume}票 ({Sign(yVolumeChange)} {Arrow(yVolumeChange)})",
                $"**昨日收入:** ￥{yRevenue:N0} ({SignPct(yRevenueChangePct)} {ArrowD(yRevenueChangePct)})",
                $"**昨日成本:** ￥{yCost:N0} ({SignPct(yCostChangePct)} {ArrowD(-yCostChangePct)})",
                $"**昨日利润:** ￥{yProfit:N0} ({SignPct(yProfitChangePct)} {ArrowD(yProfitChangePct)})",
                "",
                "---",
                "",
                "**本月累计:**",
                $"票量 {mVolume:N0} | 收入 ￥{mRevenue:N0}",
                $"成本 ￥{mCost:N0} | 利润 ￥{mProfit:N0}"
            );

            var domain = _config["DingTalk:RedirectDomain"] ?? "http://localhost:9000";
            var linkUrl = $"{domain}/redirect/dashboard";

            var result = await _botService.SendActionCard(title, text, "查看详情", linkUrl);
            if (result.Success)
            {
                _logger.LogInformation("[DailyReportBot] 每日晨报推送成功");
            }
            else
            {
                _logger.LogWarning("[DailyReportBot] 每日晨报推送失败: {Msg}", result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DailyReportBot] 执行失败");
            throw;
        }
    }

    private static string Sign(int v) => v >= 0 ? $"+{v}" : v.ToString();
    private static string SignPct(double v) => (v >= 0 ? "+" : "") + v.ToString("F1") + "%";
    private static string Arrow(int v) => v >= 0 ? "↑" : "↓";
    private static string ArrowD(double v) => v >= 0 ? "↑" : "↓";
}
