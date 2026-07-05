using System.Text;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;
using STOTOP.WebAPI.Services;

namespace STOTOP.WebAPI.Jobs;

/// <summary>
/// 异常告警机器人推送 — Hangfire 每小时轮询，亦可由业务事件实时触发。
/// 多客户 per-tenant 分发：逐活跃租户各自检查、推送到其群机器人。
/// 检查项：1. 成本率是否超阈值；2. 待办超时。（当前检查逻辑为占位示例，真实取数待 M8+ 对接。）
/// </summary>
[AutomaticRetry(Attempts = 1)]
public class AlertBotJob
{
    private readonly DingTalkBotService _botService;
    private readonly IConfiguration _config;
    private readonly ITenantIterationService _iteration;
    private readonly BotWebhookResolver _webhookResolver;
    private readonly ILogger<AlertBotJob> _logger;

    public AlertBotJob(
        DingTalkBotService botService,
        IConfiguration config,
        ITenantIterationService iteration,
        BotWebhookResolver webhookResolver,
        ILogger<AlertBotJob> logger)
    {
        _botService = botService;
        _config = config;
        _iteration = iteration;
        _webhookResolver = webhookResolver;
        _logger = logger;
    }

    public async Task Execute()
    {
        _logger.LogInformation("[AlertBot] 开始执行异常告警检查...");

        // 多客户 per-tenant 分发：逐活跃租户各自检查 + 推送到其群机器人。
        // 单客户下只循环 1 次（= 根租户，webhook 回退 appsettings），行为不变。单租户失败由地基隔离并记日志。
        await _iteration.ForEachActiveTenantAsync(async tid =>
        {
            var (webhook, secret) = await _webhookResolver.ResolveAsync(tid);
            if (string.IsNullOrWhiteSpace(webhook))
                return;  // 该租户未配置群机器人 → 跳过

            var alerts = new List<string>();

            // 配置阈值
            double costRateThreshold = _config.GetValue<double?>("DingTalk:Alert:CostRateThreshold") ?? 0.85;
            int overdueHours = _config.GetValue<int?>("DingTalk:Alert:WorkItemOverdueHours") ?? 48;

            // TODO(M8+): 检查项对接 Finance/Amoeba（成本率）+ Workflow.WfWorkItem（待办超时），按当前租户上下文取数。
            //           迭代已设 CurrentTenantId=tid，届时业务查询自动按租户收敛。此处仍为占位示例数据。
            double currentCostRate = 0.83;
            if (currentCostRate >= costRateThreshold)
                alerts.Add($"- ⚠️ **成本率告警** 当前成本率 {currentCostRate:P1}，已达阈值 {costRateThreshold:P0}");

            int overdueCount = 0;
            if (overdueCount > 0)
                alerts.Add($"- ⚠️ **超时待办告警:** 共 {overdueCount} 条待办超过 {overdueHours} 小时未处理");

            if (alerts.Count == 0)
                return;  // 该租户本次无告警

            var sb = new StringBuilder();
            sb.AppendLine("## 🚨 系统异常告警");
            sb.AppendLine();
            sb.AppendLine($"**告警时间:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            foreach (var line in alerts) sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine("> 请相关同事尽快关注并处理。");

            var result = await _botService.SendMarkdown("STOTOP 系统告警", sb.ToString(), webhook, secret);
            if (result.Success)
                _logger.LogInformation("[AlertBot] 租户 {Tid} 告警推送成功，共 {Count} 项", tid, alerts.Count);
            else
                _logger.LogWarning("[AlertBot] 租户 {Tid} 告警推送失败: {Msg}", tid, result.Message);
        }, "dingtalk-alert-check");
    }
}
