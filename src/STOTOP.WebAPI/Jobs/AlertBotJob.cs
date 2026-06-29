using System.Text;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using STOTOP.WebAPI.Services;

namespace STOTOP.WebAPI.Jobs;

/// <summary>
/// 异常告警机器人推送 — Hangfire 每小时轮询，亦可由业务事件实时触发
/// 检查项：
/// 1. 成本率是否超过阈值（默认 85%）
/// 2. 待办超时（超过 48 小时未处理）
/// </summary>
[AutomaticRetry(Attempts = 1)]
public class AlertBotJob
{
    private readonly DingTalkBotService _botService;
    private readonly IConfiguration _config;
    private readonly ILogger<AlertBotJob> _logger;

    public AlertBotJob(
        DingTalkBotService botService,
        IConfiguration config,
        ILogger<AlertBotJob> logger)
    {
        _botService = botService;
        _config = config;
        _logger = logger;
    }

    public async Task Execute()
    {
        _logger.LogInformation("[AlertBot] 开始执行异常告警检查...");

        try
        {
            var alerts = new List<string>();

            // 配置阈值
            double costRateThreshold = _config.GetValue<double?>("DingTalk:Alert:CostRateThreshold") ?? 0.85;
            int overdueHours = _config.GetValue<int?>("DingTalk:Alert:WorkItemOverdueHours") ?? 48;

            // TODO: 1. 检查成本率（对接 Finance / Amoeba 模块）
            // 模拟示例数据：
            double currentCostRate = 0.83; // 当前成本率（示例）
            if (currentCostRate >= costRateThreshold)
            {
                alerts.Add($"- ⚠️ **成本率告警** 当前成本率 {currentCostRate:P1}，已达阈值 {costRateThreshold:P0}");
            }

            // TODO: 2. 检查待办超时（对接 Workflow.WfWorkItem，FCreateTime + overdueHours < now，未完成）
            int overdueCount = 0; // 示例
            if (overdueCount > 0)
            {
                alerts.Add($"- ⚠️ **超时待办告警:** 共 {overdueCount} 条待办超过 {overdueHours} 小时未处理");
            }

            if (alerts.Count == 0)
            {
                _logger.LogInformation("[AlertBot] 本次检查无告警，跳过推送");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("## 🚨 系统异常告警");
            sb.AppendLine();
            sb.AppendLine($"**告警时间:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            foreach (var line in alerts) sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine("> 请相关同事尽快关注并处理。");

            var result = await _botService.SendMarkdown("STOTOP 系统告警", sb.ToString());
            if (result.Success)
            {
                _logger.LogInformation("[AlertBot] 告警推送成功，共 {Count} 项", alerts.Count);
            }
            else
            {
                _logger.LogWarning("[AlertBot] 告警推送失败: {Msg}", result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AlertBot] 执行失败");
            throw;
        }
    }
}
