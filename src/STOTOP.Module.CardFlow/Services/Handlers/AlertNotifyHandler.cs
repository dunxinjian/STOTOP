using System.Data;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.CardFlow.Services.Handlers;

public class AlertNotifyHandler : IClassificationHandler
{
    private readonly ILogger<AlertNotifyHandler> _logger;
    private readonly STOTOPDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;

    public string HandlerType => "AlertNotify";

    public AlertNotifyHandler(
        ILogger<AlertNotifyHandler> logger,
        STOTOPDbContext dbContext,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HandlerResult> HandleAsync(HandlerContext context)
    {
        // 1. 反序列化 HandlerConfig
        AlertNotifyConfig? config = null;
        if (!string.IsNullOrWhiteSpace(context.HandlerConfig))
        {
            try
            {
                config = JsonSerializer.Deserialize<AlertNotifyConfig>(context.HandlerConfig,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AlertNotifyHandler: HandlerConfig 反序列化失败");
            }
        }

        config ??= new AlertNotifyConfig();

        // 2. 模板变量替换
        var message = ReplaceTemplateVariables(config.MessageTemplate, context);

        // 3. 按通道发送
        var results = new List<string>();
        foreach (var channel in config.Channels)
        {
            switch (channel.ToLower())
            {
                case "system":
                    _logger.LogWarning("[系统告警] 收件人: {Recipients} | {Message}",
                        string.Join(",", config.Recipients), message);
                    results.Add("system:ok");
                    break;

                case "dingtalk":
                    var dtResult = await SendDingTalkAlertAsync(config.Recipients, message);
                    if (!dtResult.Success)
                        return HandlerResult.Fail($"钉钉告警发送失败: {dtResult.ErrorMessage}");
                    results.Add("dingtalk:ok");
                    break;

                default:
                    _logger.LogWarning("AlertNotifyHandler: 不支持的通道 {Channel}", channel);
                    break;
            }
        }

        return HandlerResult.Ok($"告警通知已发送: {message} ({string.Join(", ", results)})");
    }

    /// <summary>
    /// 通过钉钉工作通知 API 发送告警消息
    /// </summary>
    private async Task<(bool Success, string? ErrorMessage)> SendDingTalkAlertAsync(
        List<string> recipientUserIds, string message)
    {
        try
        {
            // 1. 读取钉钉全局配置
            var dtConfigRecord = DingTalkConfigHelper.GetGlobalConfig();
            if (dtConfigRecord == null || dtConfigRecord.IsEnabled == 0)
            {
                _logger.LogWarning("AlertNotifyHandler: 未找到已启用的钉钉配置，无法发送钉钉告警");
                return (false, "未找到已启用的钉钉配置");
            }

            var appKey = dtConfigRecord.AppKey;
            var appSecret = DingTalkConfigHelper.DecryptSecret(dtConfigRecord.AppSecret);
            if (string.IsNullOrEmpty(appSecret))
                appSecret = dtConfigRecord.AppSecret; // 兼容未加密的旧数据
            var agentId = dtConfigRecord.AgentId;

            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            // 2. 解析收件人用户ID列表
            var userIds = recipientUserIds
                .SelectMany(r => r.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(id => int.TryParse(id.Trim(), out var v) ? v : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (userIds.Count == 0)
            {
                _logger.LogWarning("AlertNotifyHandler: 收件人列表为空，无法发送钉钉告警");
                return (false, "收件人列表为空");
            }

            // 3. 查询已绑定钉钉的用户
            var boundUsers = (await connection.QueryAsync<DingTalkUserMapping>(
                "SELECT FID AS UserId, [F钉钉用户ID] AS DingTalkUserId FROM [SYS用户] WHERE FID IN @UserIds AND [F钉钉绑定状态] = 1",
                new { UserIds = userIds })).ToList();

            var boundUserIds = boundUsers.Select(u => u.UserId).ToHashSet();
            var unboundUserIds = userIds.Where(id => !boundUserIds.Contains(id)).ToList();

            if (unboundUserIds.Count > 0)
            {
                _logger.LogWarning("AlertNotifyHandler: 以下用户未绑定钉钉: {UnboundUserIds}",
                    string.Join(",", unboundUserIds));
            }

            var validDingTalkUserIds = boundUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.DingTalkUserId))
                .Select(u => u.DingTalkUserId!)
                .ToList();

            if (validDingTalkUserIds.Count == 0)
            {
                _logger.LogWarning("AlertNotifyHandler: 无有效钉钉绑定用户，无法发送钉钉告警。未绑定用户: {UnboundUserIds}",
                    string.Join(",", userIds));
                return (false, $"无有效钉钉绑定用户，未绑定用户ID: {string.Join(",", userIds)}");
            }

            // 4. 获取 AccessToken
            var httpClient = _httpClientFactory.CreateClient();
            var tokenUrl = $"https://oapi.dingtalk.com/gettoken?appkey={appKey}&appsecret={appSecret}";
            var tokenResp = await httpClient.GetFromJsonAsync<JsonElement>(tokenUrl);

            var errcode = tokenResp.GetProperty("errcode").GetInt32();
            if (errcode != 0)
            {
                var errmsg = tokenResp.TryGetProperty("errmsg", out var m) ? m.GetString() : "未知错误";
                _logger.LogError("AlertNotifyHandler: 获取钉钉AccessToken失败, errcode={ErrCode}, errmsg={ErrMsg}", errcode, errmsg);
                return (false, $"获取钉钉AccessToken失败: {errmsg}");
            }

            var accessToken = tokenResp.GetProperty("access_token").GetString();

            // 5. 发送工作通知
            var sendUrl = $"https://oapi.dingtalk.com/topapi/message/corpconversation/asyncsend_v2?access_token={accessToken}";
            var payload = new
            {
                agent_id = agentId,
                userid_list = string.Join(",", validDingTalkUserIds),
                msg = new
                {
                    msgtype = "text",
                    text = new { content = message }
                }
            };

            var sendResp = await httpClient.PostAsJsonAsync(sendUrl, payload);
            var sendResult = await sendResp.Content.ReadFromJsonAsync<JsonElement>();

            var sendErrCode = sendResult.GetProperty("errcode").GetInt32();
            if (sendErrCode != 0)
            {
                var sendErrMsg = sendResult.TryGetProperty("errmsg", out var sm) ? sm.GetString() : "未知错误";
                _logger.LogError("AlertNotifyHandler: 钉钉工作通知发送失败, errcode={ErrCode}, errmsg={ErrMsg}", sendErrCode, sendErrMsg);
                return (false, $"钉钉工作通知发送失败: {sendErrMsg}");
            }

            _logger.LogInformation("AlertNotifyHandler: 钉钉告警发送成功, 收件人: {UserIds}",
                string.Join(",", validDingTalkUserIds));
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AlertNotifyHandler: 发送钉钉告警时发生异常");
            return (false, $"发送钉钉告警异常: {ex.Message}");
        }
    }

    private static string ReplaceTemplateVariables(string template, HandlerContext context)
    {
        return template
            .Replace("{BatchId}", context.BatchId.ToString())
            .Replace("{TargetTable}", context.TargetTable)
            .Replace("{ClassificationType}", context.Classification.Type)
            .Replace("{AffectedRowCount}", context.Classification.AffectedRowCount.ToString())
            .Replace("{Severity}", context.Classification.Severity);
    }

    private class AlertNotifyConfig
    {
        public List<string> Channels { get; set; } = new() { "system" };
        public List<string> Recipients { get; set; } = new();
        public string MessageTemplate { get; set; } = "【数据告警】{TargetTable} 批次{BatchId}检测到{ClassificationType}，共{AffectedRowCount}条，严重级别：{Severity}";
    }

    private class DingTalkUserMapping
    {
        public int UserId { get; set; }
        public string? DingTalkUserId { get; set; }
    }
}
