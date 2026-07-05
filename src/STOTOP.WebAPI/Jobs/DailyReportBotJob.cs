using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;
using STOTOP.WebAPI.Services;

namespace STOTOP.WebAPI.Jobs;

/// <summary>
/// 每日经营晨报机器人推送 — 工作日 08:00 触发。多客户 per-tenant 分发：逐活跃租户各自聚合 + 推送到其群机器人。
/// （聚合内容当前为占位示例，真实按租户聚合 Express/Finance/Amoeba 待 M8+ 对接。）
/// </summary>
[AutomaticRetry(Attempts = 2)]
public class DailyReportBotJob
{
    private readonly DingTalkBotService _botService;
    private readonly IConfiguration _config;
    private readonly ITenantIterationService _iteration;
    private readonly BotWebhookResolver _webhookResolver;
    private readonly ILogger<DailyReportBotJob> _logger;

    public DailyReportBotJob(
        DingTalkBotService botService,
        IConfiguration config,
        ITenantIterationService iteration,
        BotWebhookResolver webhookResolver,
        ILogger<DailyReportBotJob> logger)
    {
        _botService = botService;
        _config = config;
        _iteration = iteration;
        _webhookResolver = webhookResolver;
        _logger = logger;
    }

    public async Task Execute()
    {
        _logger.LogInformation("[DailyReportBot] 开始生成每日晨报...");

        // 多客户 per-tenant 分发：逐活跃租户各自聚合 + 推送到其群机器人（单客户=1 次=根租户 appsettings webhook，零回归）。
        await _iteration.ForEachActiveTenantAsync(async tid =>
        {
            var (webhook, secret) = await _webhookResolver.ResolveAsync(tid);
            if (string.IsNullOrWhiteSpace(webhook))
                return;  // 该租户未配置群机器人 → 跳过

            var yesterday = DateTime.Today.AddDays(-1);

            // TODO(M8+): 按【当前租户】聚合昨日/本月经营数据（Express/Finance/Amoeba）。迭代已设 CurrentTenantId=tid，
            //           届时业务查询自动按租户收敛。此处仍为占位示例数据。
            var yVolume = 580;
            var yVolumeChange = 12;
            var yRevenue = 12350m;
            var yRevenueChangePct = 3.2;
            var yCost = 10800m;
            var yCostChangePct = -1.5;
            var yProfit = 1550m;
            var yProfitChangePct = 28.0;
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

            var result = await _botService.SendActionCard(title, text, "查看详情", linkUrl, webhook, secret);
            if (result.Success)
                _logger.LogInformation("[DailyReportBot] 租户 {Tid} 每日晨报推送成功", tid);
            else
                _logger.LogWarning("[DailyReportBot] 租户 {Tid} 每日晨报推送失败: {Msg}", tid, result.Message);
        }, "dingtalk-daily-report");
    }

    private static string Sign(int v) => v >= 0 ? $"+{v}" : v.ToString();
    private static string SignPct(double v) => (v >= 0 ? "+" : "") + v.ToString("F1") + "%";
    private static string Arrow(int v) => v >= 0 ? "↑" : "↓";
    private static string ArrowD(double v) => v >= 0 ? "↑" : "↓";
}
