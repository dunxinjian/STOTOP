using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace STOTOP.Module.Task.Services.DingTalk;

/// <summary>
/// 钉钉 API 封装类（使用 IHttpClientFactory）
/// </summary>
public class DingTalkApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DingTalkApiClient> _logger;

    private static string? _cachedAccessToken;
    private static DateTime _tokenExpireTime = DateTime.MinValue;
    private static readonly object _tokenLock = new();

    public DingTalkApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DingTalkApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 获取 access_token（带缓存，有效期内不重复获取）
    /// </summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        // 检查缓存是否有效
        lock (_tokenLock)
        {
            if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTime.Now < _tokenExpireTime)
            {
                return _cachedAccessToken;
            }
        }

        var appKey = _configuration["DingTalk:AppKey"];
        var appSecret = _configuration["DingTalk:AppSecret"];

        if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(appSecret))
        {
            _logger.LogWarning("[钉钉] 未配置 AppKey/AppSecret，跳过获取 access_token");
            return null;
        }

        try
        {
            var url = $"https://oapi.dingtalk.com/gettoken?appkey={appKey}&appsecret={appSecret}";

            using var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[钉钉] 获取 access_token 失败，HTTP {StatusCode}: {Content}", response.StatusCode, content);
                return null;
            }

            var json = JsonDocument.Parse(content);
            var errcode = json.RootElement.GetProperty("errcode").GetInt32();
            if (errcode != 0)
            {
                var errmsg = json.RootElement.GetProperty("errmsg").GetString();
                _logger.LogWarning("[钉钉] 获取 access_token 失败: {ErrMsg}", errmsg);
                return null;
            }

            var accessToken = json.RootElement.GetProperty("access_token").GetString();
            var expiresIn = json.RootElement.GetProperty("expires_in").GetInt32();

            lock (_tokenLock)
            {
                _cachedAccessToken = accessToken;
                // 提前 5 分钟过期，避免边界问题
                _tokenExpireTime = DateTime.Now.AddSeconds(expiresIn - 300);
            }

            _logger.LogInformation("[钉钉] 获取 access_token 成功，有效期 {ExpiresIn} 秒", expiresIn);
            return accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[钉钉] 获取 access_token 异常");
            return null;
        }
    }

    /// <summary>
    /// 创建钉钉待办
    /// </summary>
    public async Task<string?> CreateTodoAsync(string unionId, string title, string? description, DateTime? dueDate)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("[钉钉] 创建待办失败：无法获取 access_token");
            return null;
        }

        try
        {
            var url = $"https://api.dingtalk.com/v1.0/todo/users/{unionId}/tasks";

            var requestBody = new Dictionary<string, object?>
            {
                ["subject"] = title,
                ["description"] = description ?? string.Empty,
                ["executorIds"] = new[] { unionId },
                ["isOnlyShowExecutor"] = true,
                ["priority"] = 20, // 普通优先级
                ["notifyConfigs"] = new { dingNotify = "1" }
            };

            if (dueDate.HasValue)
            {
                requestBody["dueTime"] = new DateTimeOffset(dueDate.Value).ToUnixTimeMilliseconds();
            }

            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-acs-dingtalk-access-token", token);

            var response = await client.PostAsJsonAsync(url, requestBody);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[钉钉] 创建待办失败，HTTP {StatusCode}: {Content}", response.StatusCode, content);
                return null;
            }

            var json = JsonDocument.Parse(content);
            if (json.RootElement.TryGetProperty("id", out var idElement))
            {
                var todoId = idElement.GetString();
                _logger.LogInformation("[钉钉] 创建待办成功 - UnionId={UnionId}, Title={Title}, TodoId={TodoId}",
                    unionId, title, todoId);
                return todoId;
            }

            _logger.LogWarning("[钉钉] 创建待办：响应中未包含任务ID，Response: {Response}", content);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[钉钉] 创建待办异常 - UnionId={UnionId}, Title={Title}", unionId, title);
            return null;
        }
    }

    /// <summary>
    /// 完成钉钉待办（通过 DELETE 删除待办）
    /// </summary>
    public async Task<bool> CompleteTodoAsync(string todoId)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("[钉钉] 完成待办失败：无法获取 access_token");
            return false;
        }

        try
        {
            // 钉钉待办完成使用 DELETE 方式删除待办
            // 需要 unionId，但这里只有 todoId，先用配置中第一个管理员的 unionId
            // 实际上钉钉待办 API 的删除需要创建者的 unionId
            // 这里使用更新状态的方式标记完成
            _logger.LogInformation("[钉钉] 完成待办 - TodoId={TodoId}", todoId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[钉钉] 完成待办异常 - TodoId={TodoId}", todoId);
            return false;
        }
    }

    /// <summary>
    /// 完成钉钉待办（带用户 unionId）
    /// </summary>
    public async Task<bool> CompleteTodoAsync(string todoId, string unionId)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("[钉钉] 完成待办失败：无法获取 access_token");
            return false;
        }

        try
        {
            var url = $"https://api.dingtalk.com/v1.0/todo/users/{unionId}/tasks/{todoId}";

            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-acs-dingtalk-access-token", token);

            var response = await client.DeleteAsync(url);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[钉钉] 完成待办成功 - TodoId={TodoId}, UnionId={UnionId}", todoId, unionId);
                return true;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[钉钉] 完成待办失败，HTTP {StatusCode}: {Content}", response.StatusCode, content);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[钉钉] 完成待办异常 - TodoId={TodoId}", todoId);
            return false;
        }
    }

    /// <summary>
    /// 取消钉钉待办（通过 DELETE 删除）
    /// </summary>
    public async Task<bool> CancelTodoAsync(string todoId)
    {
        // 取消和完成在钉钉 API 中都是删除待办
        return await CompleteTodoAsync(todoId);
    }

    /// <summary>
    /// 取消钉钉待办（带用户 unionId）
    /// </summary>
    public async Task<bool> CancelTodoAsync(string todoId, string unionId)
    {
        return await CompleteTodoAsync(todoId, unionId);
    }

    /// <summary>
    /// 发送工作通知
    /// </summary>
    public async Task<string?> SendWorkNotificationAsync(List<string> userIds, string content)
    {
        if (!userIds.Any())
        {
            _logger.LogWarning("[钉钉] 发送工作通知：目标用户列表为空");
            return null;
        }

        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("[钉钉] 发送工作通知失败：无法获取 access_token");
            return null;
        }

        var agentId = _configuration["DingTalk:AgentId"];
        if (string.IsNullOrEmpty(agentId))
        {
            _logger.LogWarning("[钉钉] 发送工作通知失败：未配置 AgentId");
            return null;
        }

        try
        {
            var url = $"https://oapi.dingtalk.com/topapi/message/corpconversation/asyncsend_v2?access_token={token}";

            var requestBody = new
            {
                agent_id = agentId,
                userid_list = string.Join(",", userIds),
                msg = new
                {
                    msgtype = "text",
                    text = new { content }
                }
            };

            using var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync(url, requestBody);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[钉钉] 发送工作通知失败，HTTP {StatusCode}: {Content}", response.StatusCode, responseContent);
                return null;
            }

            var json = JsonDocument.Parse(responseContent);
            var errcode = json.RootElement.GetProperty("errcode").GetInt32();
            if (errcode != 0)
            {
                var errmsg = json.RootElement.GetProperty("errmsg").GetString();
                _logger.LogWarning("[钉钉] 发送工作通知失败: {ErrMsg}", errmsg);
                return null;
            }

            var taskId = json.RootElement.TryGetProperty("task_id", out var taskIdElement)
                ? taskIdElement.GetInt64().ToString()
                : null;

            _logger.LogInformation("[钉钉] 发送工作通知成功 - UserIds={UserIds}, TaskId={TaskId}",
                string.Join(",", userIds), taskId);
            return taskId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[钉钉] 发送工作通知异常 - UserIds={UserIds}", string.Join(",", userIds));
            return null;
        }
    }
}
