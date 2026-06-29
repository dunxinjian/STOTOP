using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace STOTOP.WebAPI.Services;

/// <summary>
/// 钉钉群机器人 Webhook 发送服务（支持 HMAC-SHA256 加签）
/// </summary>
public class DingTalkBotService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<DingTalkBotService> _logger;

    // 简单的内存历史记录（最近 100 条）
    private static readonly ConcurrentQueue<BotPushHistoryItem> _history = new();
    private const int MaxHistory = 100;

    public DingTalkBotService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<DingTalkBotService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 发送原始消息体到钉钉机器人 Webhook（带加签）
    /// </summary>
    public async Task<BotSendResult> SendMessage(object messageBody, string? overrideWebhook = null, string? overrideSecret = null)
    {
        var webhookUrl = overrideWebhook ?? _config["DingTalk:RobotWebhookUrl"];
        var secret = overrideSecret ?? _config["DingTalk:RobotSecret"];

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            const string err = "钉钉机器人 Webhook 未配置";
            _logger.LogWarning(err);
            RecordHistory(false, err, messageBody, null);
            return new BotSendResult { Success = false, Message = err };
        }

        try
        {
            string url = webhookUrl;
            if (!string.IsNullOrWhiteSpace(secret))
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                var stringToSign = $"{timestamp}\n{secret}";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                var signBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
                var sign = Uri.EscapeDataString(Convert.ToBase64String(signBytes));
                var sep = webhookUrl.Contains('?') ? "&" : "?";
                url = $"{webhookUrl}{sep}timestamp={timestamp}&sign={sign}";
            }

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var response = await client.PostAsJsonAsync(url, messageBody);
            var content = await response.Content.ReadAsStringAsync();

            // 钉钉返回 errcode=0 视为成功
            bool success = false;
            string? errMsg = null;
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("errcode", out var errCodeEl)
                    && errCodeEl.GetInt32() == 0)
                {
                    success = true;
                }
                else if (doc.RootElement.TryGetProperty("errmsg", out var errMsgEl))
                {
                    errMsg = errMsgEl.GetString();
                }
            }
            catch
            {
                success = response.IsSuccessStatusCode;
            }

            if (success)
            {
                _logger.LogInformation("钉钉机器人推送成功，响应={Resp}", content);
            }
            else
            {
                _logger.LogWarning("钉钉机器人推送失败，HTTP={Status}，响应={Resp}", response.StatusCode, content);
            }

            RecordHistory(success, errMsg ?? content, messageBody, content);
            return new BotSendResult
            {
                Success = success,
                Message = errMsg ?? (success ? "ok" : content),
                RawResponse = content
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "钉钉机器人推送异常");
            RecordHistory(false, ex.Message, messageBody, null);
            return new BotSendResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// 发送 ActionCard 消息（带跳转链接）
    /// </summary>
    public Task<BotSendResult> SendActionCard(string title, string markdown, string linkTitle, string linkUrl)
    {
        var body = new
        {
            msgtype = "actionCard",
            actionCard = new
            {
                title,
                text = markdown,
                singleTitle = linkTitle,
                singleURL = linkUrl
            }
        };
        return SendMessage(body);
    }

    /// <summary>
    /// 发送 Markdown 消息
    /// </summary>
    public Task<BotSendResult> SendMarkdown(string title, string content)
    {
        var body = new
        {
            msgtype = "markdown",
            markdown = new { title, text = content }
        };
        return SendMessage(body);
    }

    /// <summary>
    /// 发送简单文本消息
    /// </summary>
    public Task<BotSendResult> SendText(string text)
    {
        var body = new
        {
            msgtype = "text",
            text = new { content = text }
        };
        return SendMessage(body);
    }

    /// <summary>
    /// 获取最近推送历史（倒序，最新在前）
    /// </summary>
    public IReadOnlyList<BotPushHistoryItem> GetHistory(int take = 50)
    {
        return _history.ToArray().Reverse().Take(take).ToList();
    }

    private static void RecordHistory(bool success, string message, object? body, string? response)
    {
        var item = new BotPushHistoryItem
        {
            Time = DateTime.Now,
            Success = success,
            Message = message ?? string.Empty,
            BodyPreview = TryPreview(body),
            Response = response
        };
        _history.Enqueue(item);
        while (_history.Count > MaxHistory && _history.TryDequeue(out _)) { }
    }

    private static string? TryPreview(object? body)
    {
        if (body == null) return null;
        try
        {
            var json = JsonSerializer.Serialize(body);
            return json.Length > 500 ? json[..500] + "..." : json;
        }
        catch { return null; }
    }
}

public class BotSendResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? RawResponse { get; set; }
}

public class BotPushHistoryItem
{
    public DateTime Time { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? BodyPreview { get; set; }
    public string? Response { get; set; }
}
