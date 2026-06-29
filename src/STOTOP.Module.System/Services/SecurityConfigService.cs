using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Services;

public class SecurityConfigService
{
    private readonly IConfiguration _configuration;

    public SecurityConfigService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string GetConnectionString()
    {
        var encryptionKey = _configuration.GetValue<string>("Security:EncryptionKey");
        return DbConnectionsHelper.GetSystemConnectionString(encryptionKey)
            ?? throw new InvalidOperationException("系统数据库连接字符串未配置");
    }

    /// <summary>
    /// 获取单项配置
    /// </summary>
    public async Task<string?> GetConfig(string key)
    {
        using var conn = new SqlConnection(GetConnectionString());
        return await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT FConfigValue FROM [SYS安全配置] WHERE FConfigKey = @Key",
            new { Key = key });
    }

    /// <summary>
    /// 获取全部安全配置
    /// </summary>
    public async Task<List<SysSecurityConfig>> GetAllConfigs()
    {
        using var conn = new SqlConnection(GetConnectionString());
        var result = await conn.QueryAsync<SysSecurityConfig>(
            "SELECT FID, FConfigKey, FConfigValue, FDescription, FUpdateTime, FUpdatedBy FROM [SYS安全配置]");
        return result.ToList();
    }

    /// <summary>
    /// 更新配置
    /// </summary>
    public async Task UpdateConfig(string key, string value, string? updatedBy = null)
    {
        using var conn = new SqlConnection(GetConnectionString());
        var affected = await conn.ExecuteAsync(
            @"UPDATE [SYS安全配置] SET FConfigValue = @Value, FUpdateTime = GETDATE(), FUpdatedBy = @UpdatedBy 
              WHERE FConfigKey = @Key",
            new { Key = key, Value = value, UpdatedBy = updatedBy });

        if (affected == 0)
        {
            await conn.ExecuteAsync(
                @"INSERT INTO [SYS安全配置] (FConfigKey, FConfigValue, FUpdateTime, FUpdatedBy)
                  VALUES (@Key, @Value, GETDATE(), @UpdatedBy)",
                new { Key = key, Value = value, UpdatedBy = updatedBy });
        }
    }

    /// <summary>
    /// 批量更新配置
    /// </summary>
    public async Task UpdateConfigs(Dictionary<string, string> configs, string? updatedBy = null)
    {
        foreach (var kv in configs)
        {
            await UpdateConfig(kv.Key, kv.Value, updatedBy);
        }
    }

    /// <summary>
    /// 获取整型配置值（带默认值）
    /// </summary>
    public async Task<int> GetIntConfig(string key, int defaultValue)
    {
        var value = await GetConfig(key);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// 获取布尔配置值（带默认值）
    /// </summary>
    public async Task<bool> GetBoolConfig(string key, bool defaultValue)
    {
        var value = await GetConfig(key);
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取字符串配置值（带默认值）
    /// </summary>
    public async Task<string> GetStringConfig(string key, string defaultValue)
    {
        var value = await GetConfig(key);
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    /// <summary>
    /// 获取供前端使用的安全配置摘要（仅超时相关配置）
    /// </summary>
    public async Task<object> GetClientConfig()
    {
        using var conn = new SqlConnection(GetConnectionString());
        var configs = await conn.QueryAsync<SysSecurityConfig>(
            @"SELECT FConfigKey, FConfigValue FROM [SYS安全配置] 
              WHERE FConfigKey LIKE 'timeout.%' OR FConfigKey LIKE 'token.%' OR FConfigKey LIKE 'session.%'");

        var dict = configs.ToDictionary(c => c.FConfigKey, c => c.FConfigValue);
        return new
        {
            idleTimeoutMinutes = GetIntValue(dict, "timeout.idle_minutes", 30),
            lockScreenMinutes = GetIntValue(dict, "timeout.lock_screen_minutes", 15),
            accessTokenMinutes = GetIntValue(dict, "token.access_token_minutes", 30),
            maxDevices = GetIntValue(dict, "session.max_devices", 5)
        };
    }

    private static int GetIntValue(Dictionary<string, string> dict, string key, int defaultValue)
    {
        return dict.TryGetValue(key, out var value) && int.TryParse(value, out var result)
            ? result : defaultValue;
    }
}
