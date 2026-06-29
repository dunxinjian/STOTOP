using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using STOTOP.WebAPI.Services;

namespace STOTOP.WebAPI.Controllers.Mobile;

/// <summary>
/// 钉钉机器人推送管理 Controller
/// </summary>
[ApiController]
[Route("api/mobile/bot")]
[Authorize]
public class BotPushController : ControllerBase
{
    private readonly DingTalkBotService _botService;
    private readonly IConfiguration _config;
    private readonly ILogger<BotPushController> _logger;

    // 进程内运行时配置覆盖（持久化方案待对接系统配置模块）
    private static readonly ConcurrentDictionary<string, object?> _runtimeConfig = new();

    public BotPushController(
        DingTalkBotService botService,
        IConfiguration config,
        ILogger<BotPushController> logger)
    {
        _botService = botService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 获取机器人配置
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var cfg = new BotConfigDto
        {
            WebhookUrl = GetCfgString("WebhookUrl", _config["DingTalk:RobotWebhookUrl"] ?? string.Empty),
            Secret = MaskSecret(GetCfgString("Secret", _config["DingTalk:RobotSecret"] ?? string.Empty)),
            EnableDaily = GetCfgBool("EnableDaily", true),
            EnableWeekly = GetCfgBool("EnableWeekly", true),
            EnableAlert = GetCfgBool("EnableAlert", true),
            CostRateThreshold = GetCfgDouble("CostRateThreshold",
                _config.GetValue<double?>("DingTalk:Alert:CostRateThreshold") ?? 0.85),
            WorkItemOverdueHours = GetCfgInt("WorkItemOverdueHours",
                _config.GetValue<int?>("DingTalk:Alert:WorkItemOverdueHours") ?? 48)
        };

        return Ok(new { code = 200, data = cfg, message = "ok" });
    }

    /// <summary>
    /// 更新机器人配置
    /// </summary>
    [HttpPut("config")]
    public IActionResult UpdateConfig([FromBody] BotConfigDto dto)
    {
        if (dto == null)
        {
            return BadRequest(new { code = 400, message = "请求体不能为空" });
        }

        if (!string.IsNullOrEmpty(dto.WebhookUrl))
            _runtimeConfig["WebhookUrl"] = dto.WebhookUrl;

        // 仅当传入非掩码值时更新 Secret
        if (!string.IsNullOrEmpty(dto.Secret) && !dto.Secret.Contains('*'))
            _runtimeConfig["Secret"] = dto.Secret;

        _runtimeConfig["EnableDaily"] = dto.EnableDaily;
        _runtimeConfig["EnableWeekly"] = dto.EnableWeekly;
        _runtimeConfig["EnableAlert"] = dto.EnableAlert;
        _runtimeConfig["CostRateThreshold"] = dto.CostRateThreshold;
        _runtimeConfig["WorkItemOverdueHours"] = dto.WorkItemOverdueHours;

        _logger.LogInformation("钉钉机器人配置已更新 by user={User}", User?.Identity?.Name);
        return Ok(new { code = 200, message = "已保存" });
    }

    /// <summary>
    /// 发送测试消息
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> SendTest([FromBody] BotTestRequest? request = null)
    {
        var webhook = GetCfgString("WebhookUrl", _config["DingTalk:RobotWebhookUrl"] ?? string.Empty);
        var secret = GetCfgString("Secret", _config["DingTalk:RobotSecret"] ?? string.Empty);

        if (string.IsNullOrWhiteSpace(webhook))
        {
            return Ok(new { code = 400, message = "请先配置 Webhook URL" });
        }

        var title = "STOTOP 机器人测试消息";
        var content = request?.Content ?? string.Join("\n",
            "## ✅ 测试消息",
            "",
            $"- 发送时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"- 触发用户: {User?.Identity?.Name ?? "system"}",
            "",
            "如果你收到此消息，说明机器人 Webhook 配置正确。"
        );

        var body = new
        {
            msgtype = "markdown",
            markdown = new { title, text = content }
        };

        var result = await _botService.SendMessage(body, webhook, secret);
        return Ok(new
        {
            code = result.Success ? 200 : 500,
            data = new { result.Success, result.RawResponse },
            message = result.Message
        });
    }

    /// <summary>
    /// 推送历史记录
    /// </summary>
    [HttpGet("history")]
    public IActionResult GetHistory([FromQuery] int take = 50)
    {
        if (take <= 0 || take > 100) take = 50;
        var list = _botService.GetHistory(take);
        return Ok(new { code = 200, data = list, message = "ok" });
    }

    // ──────────── helpers ────────────

    private static string GetCfgString(string key, string defaultVal)
        => _runtimeConfig.TryGetValue(key, out var v) && v is string s ? s : defaultVal;

    private static bool GetCfgBool(string key, bool defaultVal)
        => _runtimeConfig.TryGetValue(key, out var v) && v is bool b ? b : defaultVal;

    private static double GetCfgDouble(string key, double defaultVal)
        => _runtimeConfig.TryGetValue(key, out var v) && v is double d ? d : defaultVal;

    private static int GetCfgInt(string key, int defaultVal)
        => _runtimeConfig.TryGetValue(key, out var v) && v is int i ? i : defaultVal;

    private static string MaskSecret(string secret)
    {
        if (string.IsNullOrEmpty(secret)) return string.Empty;
        if (secret.Length <= 6) return new string('*', secret.Length);
        return secret[..3] + new string('*', secret.Length - 6) + secret[^3..];
    }
}

public class BotConfigDto
{
    public string WebhookUrl { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public bool EnableDaily { get; set; } = true;
    public bool EnableWeekly { get; set; } = true;
    public bool EnableAlert { get; set; } = true;
    public double CostRateThreshold { get; set; } = 0.85;
    public int WorkItemOverdueHours { get; set; } = 48;
}

public class BotTestRequest
{
    public string? Content { get; set; }
}
