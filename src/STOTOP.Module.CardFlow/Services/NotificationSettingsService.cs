using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 通知渠道配置服务（key-value 存储到 [CF通知配置]，按组织隔离）
/// </summary>
public class NotificationSettingsService : INotificationSettingsService
{
    // 配置键常量
    private const string KeyDingtalkAppKey = "dingtalk.appKey";
    private const string KeyDingtalkAppSecret = "dingtalk.appSecret";
    private const string KeyDingtalkAgentId = "dingtalk.agentId";
    private const string KeyDingtalkEnabled = "dingtalk.enabled";
    private const string KeyDetailUrlTemplate = "dingtalk.detailUrlTemplate";

    private readonly STOTOPDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationSettingsService> _logger;

    public NotificationSettingsService(
        STOTOPDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationSettingsService> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<NotificationSettingsDto> GetAsync(long orgId)
    {
        var configs = await _dbContext.Set<CfNotificationConfig>()
            .Where(c => c.FOrgId == orgId || c.FOrgId == 0)
            .ToListAsync();

        var dict = configs
            .GroupBy(c => c.FConfigKey)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.FOrgId).First().FConfigValue);

        return new NotificationSettingsDto
        {
            DingtalkAppKey = dict.GetValueOrDefault(KeyDingtalkAppKey),
            DingtalkAppSecret = dict.GetValueOrDefault(KeyDingtalkAppSecret),
            DingtalkAgentId = dict.GetValueOrDefault(KeyDingtalkAgentId),
            DingtalkEnabled = bool.TryParse(dict.GetValueOrDefault(KeyDingtalkEnabled), out var en) && en,
            DetailUrlTemplate = dict.GetValueOrDefault(KeyDetailUrlTemplate),
        };
    }

    public async Task SaveAsync(SaveNotificationSettingsRequest request, long orgId, long operatorId)
    {
        var pairs = new Dictionary<string, string?>();
        if (request.DingtalkAppKey != null) pairs[KeyDingtalkAppKey] = request.DingtalkAppKey;
        if (request.DingtalkAppSecret != null) pairs[KeyDingtalkAppSecret] = request.DingtalkAppSecret;
        if (request.DingtalkAgentId != null) pairs[KeyDingtalkAgentId] = request.DingtalkAgentId;
        if (request.DingtalkEnabled.HasValue) pairs[KeyDingtalkEnabled] = request.DingtalkEnabled.Value.ToString();
        if (request.DetailUrlTemplate != null) pairs[KeyDetailUrlTemplate] = request.DetailUrlTemplate;

        if (pairs.Count == 0) return;

        var keys = pairs.Keys.ToList();
        var existing = await _dbContext.Set<CfNotificationConfig>()
            .Where(c => c.FOrgId == orgId && keys.Contains(c.FConfigKey))
            .ToListAsync();
        var existingMap = existing.ToDictionary(e => e.FConfigKey);

        var now = DateTime.Now;
        foreach (var (key, value) in pairs)
        {
            if (existingMap.TryGetValue(key, out var entity))
            {
                entity.FConfigValue = value;
                entity.FUpdatedTime = now;
            }
            else
            {
                _dbContext.Set<CfNotificationConfig>().Add(new CfNotificationConfig
                {
                    FConfigKey = key,
                    FConfigValue = value,
                    FCreatedTime = now,
                    FUpdatedTime = now,
                    FOrgId = orgId
                });
            }
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("通知配置已更新, OrgId={OrgId}, Operator={OperatorId}, Keys={Keys}",
            orgId, operatorId, string.Join(",", keys));
    }

    public async Task<TestNotificationResult> TestAsync(TestNotificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Channel))
            return new TestNotificationResult { Success = false, Message = "未指定渠道" };

        if (request.Channel.Equals("dingtalk", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.AppKey) ||
                string.IsNullOrWhiteSpace(request.AppSecret) ||
                string.IsNullOrWhiteSpace(request.AgentId))
            {
                return new TestNotificationResult
                {
                    Success = false,
                    Message = "AppKey/AppSecret/AgentId 不可为空"
                };
            }

            try
            {
                // 调用钉钉 gettoken 接口验证凭证
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                var url = $"https://oapi.dingtalk.com/gettoken?appkey={Uri.EscapeDataString(request.AppKey!)}&appsecret={Uri.EscapeDataString(request.AppSecret!)}";
                var response = await httpClient.GetAsync(url);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return new TestNotificationResult
                    {
                        Success = false,
                        Message = $"HTTP {(int)response.StatusCode}: {body}"
                    };
                }

                using var doc = global::System.Text.Json.JsonDocument.Parse(body);
                var root = doc.RootElement;
                var errcode = root.TryGetProperty("errcode", out var ec) ? ec.GetInt32() : -1;
                if (errcode == 0)
                {
                    return new TestNotificationResult
                    {
                        Success = true,
                        Message = "连接成功，已获取访问令牌"
                    };
                }

                var errmsg = root.TryGetProperty("errmsg", out var em) ? em.GetString() : "未知错误";
                return new TestNotificationResult
                {
                    Success = false,
                    Message = $"errcode={errcode}, errmsg={errmsg}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "钉钉测试连接失败");
                return new TestNotificationResult
                {
                    Success = false,
                    Message = $"网络异常: {ex.Message}"
                };
            }
        }

        return new TestNotificationResult { Success = false, Message = $"暂不支持渠道: {request.Channel}" };
    }
}
