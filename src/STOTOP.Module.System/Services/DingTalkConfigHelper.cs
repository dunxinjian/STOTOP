using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace STOTOP.Module.System.Services;

/// <summary>
/// 钉钉配置 JSON 文件的静态工具类，可在 DI 容器注册前使用。
/// </summary>
public static class DingTalkConfigHelper
{
    private static readonly object _fileLock = new object();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // 默认加密密钥（与 DbConnectionsHelper 保持一致）
    private static readonly string DefaultEncryptionKey = "STOTOP@2024!DefaultKey32Bytes!";

    /// <summary>
    /// 获取 dingtalk-config.json 文件路径
    /// </summary>
    public static string GetFilePath()
        => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dingtalk-config.json");

    /// <summary>
    /// 获取文件锁对象，供外部需要与文件操作同步的场景使用
    /// </summary>
    internal static object FileLock => _fileLock;

    /// <summary>
    /// 获取全局配置（OrgId == null 的第一条记录）
    /// </summary>
    public static DingTalkConfigRecord? GetGlobalConfig()
    {
        lock (_fileLock)
        {
            var configs = LoadConfigs();
            return configs.FirstOrDefault(c => c.OrgId == null);
        }
    }

    /// <summary>
    /// 获取指定组织的配置
    /// </summary>
    public static DingTalkConfigRecord? GetConfigByOrgId(long orgId)
    {
        lock (_fileLock)
        {
            var configs = LoadConfigs();
            return configs.FirstOrDefault(c => c.OrgId == orgId);
        }
    }

    /// <summary>
    /// 获取所有组织配置（OrgId != null）
    /// </summary>
    public static List<DingTalkConfigRecord> GetAllOrgConfigs()
    {
        lock (_fileLock)
        {
            var configs = LoadConfigs();
            return configs.Where(c => c.OrgId != null).ToList();
        }
    }

    /// <summary>
    /// 保存或更新配置记录。
    /// record.Id > 0 时按 Id 匹配更新；
    /// record.Id == 0 时按 OrgId 匹配（全局用 null）；
    /// 匹配不到则新增（自动分配 Id）。
    /// </summary>
    public static DingTalkConfigRecord SaveConfig(DingTalkConfigRecord record)
    {
        lock (_fileLock)
        {
            var configs = LoadConfigs();
            DingTalkConfigRecord? existing = null;

            if (record.Id > 0)
            {
                existing = configs.FirstOrDefault(c => c.Id == record.Id);
            }
            else if (record.OrgId == null)
            {
                existing = configs.FirstOrDefault(c => c.OrgId == null);
            }
            else
            {
                existing = configs.FirstOrDefault(c => c.OrgId == record.OrgId);
            }

            if (existing != null)
            {
                existing.OrgId = record.OrgId;
                existing.ConfigName = record.ConfigName;
                existing.AppKey = record.AppKey;
                existing.AppSecret = record.AppSecret;
                existing.CorpId = record.CorpId;
                existing.AgentId = record.AgentId;
                existing.Domain = record.Domain;
                existing.RobotWebhookUrl = record.RobotWebhookUrl;
                existing.RobotSecret = record.RobotSecret;
                existing.IsEnabled = record.IsEnabled;
                existing.AutoSync = record.AutoSync;
                existing.SyncCron = record.SyncCron;
                existing.LastSyncTime = record.LastSyncTime;
                existing.UpdatedTime = DateTime.Now;
                record.Id = existing.Id;
                record.UpdatedTime = existing.UpdatedTime;
            }
            else
            {
                var newId = configs.Count > 0 ? configs.Max(c => c.Id) + 1 : 1;
                record.Id = newId;
                record.UpdatedTime = DateTime.Now;
                configs.Add(record);
            }

            SaveConfigs(configs);
            return record;
        }
    }

    /// <summary>
    /// 按 Id 删除配置
    /// </summary>
    public static bool DeleteConfig(long id)
    {
        lock (_fileLock)
        {
            var configs = LoadConfigs();
            var removed = configs.RemoveAll(c => c.Id == id);
            if (removed > 0)
            {
                SaveConfigs(configs);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// AES-256 加密（统一走 SecretProtector：随机 IV 新格式，与 DbConnectionsHelper 共用同一密钥派生）
    /// </summary>
    public static string EncryptSecret(string plainText, string? encryptionKey = null)
    {
        var key = GetEncryptionKeyBytes(encryptionKey);
        return SecretProtector.Encrypt(plainText, key);
    }

    /// <summary>
    /// AES-256 解密（兼容旧的确定性 IV 格式）
    /// </summary>
    public static string DecryptSecret(string cipherText, string? encryptionKey = null)
    {
        try
        {
            var key = GetEncryptionKeyBytes(encryptionKey);
            return SecretProtector.Decrypt(cipherText, key);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 仅更新指定配置的最后同步时间（原子操作，不影响其他字段）
    /// </summary>
    public static void UpdateLastSyncTime(long configId)
    {
        lock (_fileLock)
        {
            var configs = LoadConfigs();
            var config = configs.FirstOrDefault(c => c.Id == configId);
            if (config != null)
            {
                config.LastSyncTime = DateTime.Now;
                config.UpdatedTime = DateTime.Now;
                SaveConfigs(configs);
            }
        }
    }

    /// <summary>
    /// 原子更新自动同步配置（不影响其他字段）
    /// </summary>
    public static void UpdateAutoSync(long configId, int autoSync, string? cron)
    {
        lock (_fileLock)
        {
            var configs = LoadConfigs();
            var config = configs.FirstOrDefault(c => c.Id == configId);
            if (config != null)
            {
                config.AutoSync = autoSync;
                config.SyncCron = cron;
                config.UpdatedTime = DateTime.Now;
                SaveConfigs(configs);
            }
        }
    }

    #region 内部方法

    internal static List<DingTalkConfigRecord> LoadConfigs()
    {
        var filePath = GetFilePath();
        if (!File.Exists(filePath))
            return new List<DingTalkConfigRecord>();
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<DingTalkConfigRecord>>(json, _jsonOptions)
               ?? new List<DingTalkConfigRecord>();
    }

    internal static void SaveConfigs(List<DingTalkConfigRecord> configs)
    {
        var filePath = GetFilePath();
        var json = JsonSerializer.Serialize(configs, _jsonOptions);
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, filePath, overwrite: true);
    }

    private static byte[] GetEncryptionKeyBytes(string? encryptionKey)
    {
        var keyStr = encryptionKey ?? DefaultEncryptionKey;
        return Encoding.UTF8.GetBytes(keyStr.PadRight(32, '0')[..32]);
    }

    #endregion
}

/// <summary>
/// 钉钉配置记录 POCO，与 dingtalk-config.json 中的记录对应。
/// </summary>
public class DingTalkConfigRecord
{
    public long Id { get; set; }
    public long? OrgId { get; set; }
    public string ConfigName { get; set; } = "";
    public string AppKey { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string CorpId { get; set; } = "";
    public string? AgentId { get; set; }
    public string? Domain { get; set; }
    /// <summary>群机器人 Webhook（供告警/日报/周报 bot per-tenant 分发）。</summary>
    public string? RobotWebhookUrl { get; set; }
    /// <summary>群机器人加签密钥（可选 HMAC-SHA256）。</summary>
    public string? RobotSecret { get; set; }
    public int IsEnabled { get; set; } = 1;
    public int AutoSync { get; set; }
    public string? SyncCron { get; set; } = "0 0 2 * * ?";
    public DateTime? LastSyncTime { get; set; }
    public DateTime CreatedTime { get; set; } = DateTime.Now;
    public DateTime? UpdatedTime { get; set; }
}
