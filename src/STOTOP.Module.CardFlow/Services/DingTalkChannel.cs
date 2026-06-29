using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public class DingTalkChannel : INotificationChannel
{
    private readonly ILogger<DingTalkChannel> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly INotificationSettingsService _settingsService;
    private readonly STOTOPDbContext _dbContext;

    /// <summary>
    /// 组织级 AccessToken 缓存（orgId → (token, expireTime)）
    /// </summary>
    private static readonly ConcurrentDictionary<long, (string token, DateTime expireTime)> _tokenCache = new();

    public DingTalkChannel(
        ILogger<DingTalkChannel> logger,
        IHttpClientFactory httpClientFactory,
        INotificationSettingsService settingsService,
        STOTOPDbContext dbContext)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _settingsService = settingsService;
        _dbContext = dbContext;
    }

    public string ChannelName => "dingtalk";

    public async Task<string?> CreateTodoAsync(NotificationPayload payload)
    {
        var orgId = Convert.ToInt64(payload.Extra!["orgId"]);
        var handlerId = Convert.ToInt64(payload.Extra!["handlerId"]);

        var unionId = await ResolveUnionIdAsync(handlerId);
        if (string.IsNullOrWhiteSpace(unionId))
        {
            _logger.LogWarning("[钉钉] 创建待办失败：用户 {HandlerId} 未绑定钉钉UnionId", handlerId);
            return null;
        }

        var token = await GetAccessTokenAsync(orgId);
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException($"无法获取组织 {orgId} 的钉钉 AccessToken");

        var settings = await _settingsService.GetAsync(orgId);
        var detailUrl = settings.DetailUrlTemplate?.Replace("{id}", payload.TodoItemId.ToString())
                        ?? $"/cardflow/todo/{payload.TodoItemId}";

        var url = $"https://api.dingtalk.com/v1.0/todo/users/{unionId}/tasks";

        var requestBody = new Dictionary<string, object>
        {
            ["subject"] = payload.Subject,
            ["description"] = "",
            ["executorIds"] = new[] { unionId },
            ["isOnlyShowExecutor"] = true,
            ["priority"] = 20,
            ["notifyConfigs"] = new { dingNotify = "1" },
            ["detailUrl"] = new { pcUrl = detailUrl, appUrl = detailUrl }
        };

        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-acs-dingtalk-access-token", token);

        var response = await client.PostAsJsonAsync(url, requestBody);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[钉钉] 创建待办失败，HTTP {StatusCode}: {Content}", response.StatusCode, content);
            throw new HttpRequestException($"钉钉创建待办失败: HTTP {(int)response.StatusCode}, {content}");
        }

        using var json = JsonDocument.Parse(content);
        string? taskId = null;
        if (json.RootElement.TryGetProperty("id", out var idElement))
            taskId = idElement.GetString();
        else if (json.RootElement.TryGetProperty("taskId", out var taskIdElement))
            taskId = taskIdElement.GetString();

        if (string.IsNullOrEmpty(taskId))
        {
            _logger.LogError("[钉钉] 创建待办：响应中未包含任务ID，Response: {Response}", content);
            throw new InvalidOperationException("钉钉创建待办响应中未包含任务ID");
        }

        _logger.LogInformation("[钉钉] 创建待办成功 - UnionId={UnionId}, Subject={Subject}, TaskId={TaskId}",
            unionId, payload.Subject, taskId);

        // 返回格式：orgId|unionId|taskId，便于后续完成/删除操作解析
        return $"{orgId}|{unionId}|{taskId}";
    }

    public async Task CompleteTodoAsync(string externalTodoId)
    {
        var (orgId, unionId, taskId) = ParseExternalTodoId(externalTodoId);

        var token = await GetAccessTokenAsync(orgId);
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException($"无法获取组织 {orgId} 的钉钉 AccessToken");

        var url = $"https://api.dingtalk.com/v1.0/todo/users/{unionId}/tasks/{taskId}?operatorId={unionId}";

        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-acs-dingtalk-access-token", token);

        var requestBody = new { done = true };
        var response = await client.PutAsJsonAsync(url, requestBody);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[钉钉] 完成待办失败，HTTP {StatusCode}: {Content}, TaskId={TaskId}",
                response.StatusCode, content, taskId);
            throw new HttpRequestException($"钉钉完成待办失败: HTTP {(int)response.StatusCode}, {content}");
        }

        _logger.LogInformation("[钉钉] 完成待办成功 - TaskId={TaskId}, UnionId={UnionId}", taskId, unionId);
    }

    public async Task DeleteTodoAsync(string externalTodoId)
    {
        var (orgId, unionId, taskId) = ParseExternalTodoId(externalTodoId);

        var token = await GetAccessTokenAsync(orgId);
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException($"无法获取组织 {orgId} 的钉钉 AccessToken");

        var url = $"https://api.dingtalk.com/v1.0/todo/users/{unionId}/tasks/{taskId}?operatorId={unionId}";

        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-acs-dingtalk-access-token", token);

        var response = await client.DeleteAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[钉钉] 删除待办失败，HTTP {StatusCode}: {Content}, TaskId={TaskId}",
                response.StatusCode, content, taskId);
            throw new HttpRequestException($"钉钉删除待办失败: HTTP {(int)response.StatusCode}, {content}");
        }

        _logger.LogInformation("[钉钉] 删除待办成功 - TaskId={TaskId}, UnionId={UnionId}", taskId, unionId);
    }

    public async Task<bool> ValidateCallbackAsync(CallbackContext context)
    {
        if (!context.Headers.TryGetValue("timestamp", out var timestamp) ||
            !context.Headers.TryGetValue("sign", out var sign))
        {
            _logger.LogWarning("[钉钉] 回调验签失败：缺少 timestamp 或 sign 头");
            return false;
        }

        // 需要从配置获取 appSecret，但回调不携带 orgId，尝试从 body 解析
        // 此处简单使用全局配置（orgId=0）
        var settings = await _settingsService.GetAsync(0);
        var appSecret = settings.DingtalkAppSecret;
        if (string.IsNullOrEmpty(appSecret))
        {
            _logger.LogWarning("[钉钉] 回调验签失败：未配置 appSecret");
            return false;
        }

        var stringToSign = $"{timestamp}\n{appSecret}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        var computedSign = Convert.ToBase64String(hash);

        var valid = string.Equals(computedSign, sign, StringComparison.Ordinal);
        if (!valid)
        {
            _logger.LogWarning("[钉钉] 回调验签失败：签名不匹配, Expected={Expected}, Actual={Actual}",
                computedSign, sign);
        }

        return valid;
    }

    public async Task HandleCallbackAsync(CallbackContext context)
    {
        _logger.LogInformation("[钉钉] 处理回调事件: {EventType}", context.EventType);

        try
        {
            using var doc = JsonDocument.Parse(context.RawBody);
            var root = doc.RootElement;

            switch (context.EventType)
            {
                case "todo_task_finish":
                    await HandleTodoStatusChangeAsync(root, "completed");
                    break;

                case "todo_task_delete":
                    await HandleTodoStatusChangeAsync(root, "deleted");
                    break;

                default:
                    _logger.LogInformation("[钉钉] 未处理的回调事件类型: {EventType}", context.EventType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[钉钉] 处理回调事件异常: {EventType}", context.EventType);
            throw;
        }
    }

    #region 私有方法

    /// <summary>
    /// 获取组织级 AccessToken（带缓存，提前5分钟过期）
    /// </summary>
    private async Task<string?> GetAccessTokenAsync(long orgId)
    {
        // 检查缓存
        if (_tokenCache.TryGetValue(orgId, out var cached) && DateTime.Now < cached.expireTime)
        {
            return cached.token;
        }

        var settings = await _settingsService.GetAsync(orgId);
        var appKey = settings.DingtalkAppKey;
        var appSecret = settings.DingtalkAppSecret;

        if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(appSecret))
        {
            _logger.LogWarning("[钉钉] 组织 {OrgId} 未配置 AppKey/AppSecret", orgId);
            return null;
        }

        var url = $"https://oapi.dingtalk.com/gettoken?appkey={Uri.EscapeDataString(appKey)}&appsecret={Uri.EscapeDataString(appSecret)}";

        using var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[钉钉] 获取 AccessToken 失败，HTTP {StatusCode}: {Content}", response.StatusCode, content);
            return null;
        }

        using var json = JsonDocument.Parse(content);
        var errcode = json.RootElement.GetProperty("errcode").GetInt32();
        if (errcode != 0)
        {
            var errmsg = json.RootElement.GetProperty("errmsg").GetString();
            _logger.LogError("[钉钉] 获取 AccessToken 失败: errcode={ErrCode}, errmsg={ErrMsg}", errcode, errmsg);
            return null;
        }

        var accessToken = json.RootElement.GetProperty("access_token").GetString()!;
        var expiresIn = json.RootElement.GetProperty("expires_in").GetInt32();

        // 缓存 token，提前5分钟过期
        _tokenCache[orgId] = (accessToken, DateTime.Now.AddSeconds(expiresIn - 300));

        _logger.LogInformation("[钉钉] 获取 AccessToken 成功, OrgId={OrgId}, 有效期={ExpiresIn}s", orgId, expiresIn);
        return accessToken;
    }

    /// <summary>
    /// 从 SYS用户 表查询用户的钉钉 UnionId
    /// </summary>
    private async Task<string?> ResolveUnionIdAsync(long handlerId)
    {
        var unionId = await _dbContext.Database
            .SqlQueryRaw<string>("SELECT F钉钉UnionId AS Value FROM tbl_sys_user WHERE FID = {0}", handlerId)
            .FirstOrDefaultAsync();

        return unionId;
    }

    /// <summary>
    /// 解析 externalTodoId 格式：orgId|unionId|taskId
    /// </summary>
    private static (long orgId, string unionId, string taskId) ParseExternalTodoId(string externalTodoId)
    {
        var parts = externalTodoId.Split('|');
        if (parts.Length != 3)
            throw new ArgumentException($"无效的 externalTodoId 格式: {externalTodoId}，期望格式: orgId|unionId|taskId");

        return (long.Parse(parts[0]), parts[1], parts[2]);
    }

    /// <summary>
    /// 处理待办状态变更回调
    /// </summary>
    private async Task HandleTodoStatusChangeAsync(JsonElement root, string newPushStatus)
    {
        // 尝试从回调 body 中获取 taskId
        string? taskId = null;
        if (root.TryGetProperty("taskId", out var taskIdEl))
            taskId = taskIdEl.GetString();
        else if (root.TryGetProperty("id", out var idEl))
            taskId = idEl.GetString();

        if (string.IsNullOrEmpty(taskId))
        {
            _logger.LogWarning("[钉钉] 回调缺少 taskId，无法匹配待办项");
            return;
        }

        // 通过 externalTodoId 中包含的 taskId 部分匹配
        var todoItem = await _dbContext.Set<CfTodoItem>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FExternalTodoId != null && t.FExternalTodoId.EndsWith("|" + taskId));

        if (todoItem == null)
        {
            _logger.LogWarning("[钉钉] 回调：未找到匹配的待办项, TaskId={TaskId}", taskId);
            return;
        }

        todoItem.FPushStatus = newPushStatus;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("[钉钉] 回调更新待办状态: TodoItemId={TodoItemId}, PushStatus={Status}",
            todoItem.FID, newPushStatus);
    }

    #endregion
}
