using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services;

/// <summary>
/// 数据库连接 JSON 文件的静态工具类，可在 DI 容器注册前使用。
/// </summary>
public static class DbConnectionsHelper
{
    private static readonly object _fileLock = new object();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // 默认加密密钥（与 DbConnectionService 保持一致）
    private static readonly string DefaultEncryptionKey = "STOTOP@2024!DefaultKey32Bytes!";

    /// <summary>
    /// 获取 db-connections.json 文件路径
    /// </summary>
    public static string GetFilePath()
        => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db-connections.json");

    /// <summary>
    /// 判断系统是否已初始化（= 存在系统类型的连接记录）
    /// </summary>
    public static bool IsInitialized()
    {
        var conn = GetSystemConnection();
        return conn != null;
    }

    /// <summary>
    /// 获取 Provider（从系统连接的 databaseType，默认 "SqlServer"）
    /// </summary>
    public static string GetProvider()
    {
        var conn = GetSystemConnection();
        return conn?.DatabaseType ?? "SqlServer";
    }

    /// <summary>
    /// 获取系统连接字符串（从系统连接记录构建）
    /// </summary>
    public static string? GetSystemConnectionString(string? encryptionKey = null)
    {
        var conn = GetSystemConnection();
        if (conn == null) return null;

        // 解密密码
        var password = !string.IsNullOrEmpty(conn.Password)
            ? DecryptPassword(conn.Password, encryptionKey)
            : null;

        return BuildConnectionString(
            conn.DatabaseType, conn.Server, conn.Port, conn.DatabaseName,
            conn.Username, password, conn.FilePath,
            conn.WindowsAuth, conn.TrustServerCertificate,
            conn.ConnectionString);
    }

    /// <summary>
    /// 根据 ID 获取指定连接的连接字符串（含解密密码）
    /// </summary>
    public static string? GetConnectionStringById(long id, string? encryptionKey = null)
    {
        lock (_fileLock)
        {
            var connections = LoadConnections();
            var conn = connections.FirstOrDefault(c => c.Id == id);
            if (conn == null) return null;

            var password = !string.IsNullOrEmpty(conn.Password)
                ? DecryptPassword(conn.Password, encryptionKey)
                : null;

            return BuildConnectionString(
                conn.DatabaseType, conn.Server, conn.Port, conn.DatabaseName,
                conn.Username, password, conn.FilePath,
                conn.WindowsAuth, conn.TrustServerCertificate,
                conn.ConnectionString);
        }
    }

    /// <summary>
    /// 获取系统连接记录（connectionType == "系统"），返回 null 表示未配置
    /// </summary>
    public static DbConnectionRecord? GetSystemConnection()
    {
        lock (_fileLock)
        {
            var connections = LoadConnections();
            return connections.FirstOrDefault(c => c.ConnectionType == "系统");
        }
    }

    /// <summary>
    /// 保存或更新系统连接记录。若已存在则更新，否则新增。
    /// </summary>
    public static void SaveSystemConnection(
        string server, string databaseName, string? username, string? password,
        string databaseType = "SqlServer", bool windowsAuth = false,
        bool trustServerCertificate = true, string? encryptionKey = null)
    {
        lock (_fileLock)
        {
            var connections = LoadConnections();
            var existing = connections.FirstOrDefault(c => c.ConnectionType == "系统");

            var encryptedPassword = !string.IsNullOrEmpty(password)
                ? EncryptPassword(password, encryptionKey)
                : null;

            if (existing != null)
            {
                existing.Server = server;
                existing.DatabaseName = databaseName;
                existing.Username = username;
                existing.Password = encryptedPassword;
                existing.DatabaseType = databaseType;
                existing.WindowsAuth = windowsAuth;
                existing.TrustServerCertificate = trustServerCertificate;
                existing.UpdatedTime = DateTime.Now;
            }
            else
            {
                var newId = connections.Count > 0 ? connections.Max(c => c.Id) + 1 : 1;
                connections.Add(new DbConnectionRecord
                {
                    Id = newId,
                    ConnectionName = "主数据库",
                    DatabaseType = databaseType,
                    Server = server,
                    DatabaseName = databaseName,
                    Username = username,
                    Password = encryptedPassword,
                    WindowsAuth = windowsAuth,
                    TrustServerCertificate = trustServerCertificate,
                    ConnectionType = "系统",
                    Status = 1,
                    CreatedTime = DateTime.Now
                });
            }

            SaveConnections(connections);
        }
    }

    /// <summary>
    /// 通过解析完整连接字符串来保存系统连接
    /// </summary>
    public static void SaveSystemConnectionFromConnectionString(
        string connectionString, string databaseType = "SqlServer", string? encryptionKey = null)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        SaveSystemConnection(
            server: builder.DataSource,
            databaseName: builder.InitialCatalog,
            username: builder.UserID,
            password: builder.Password,
            databaseType: databaseType,
            windowsAuth: builder.IntegratedSecurity,
            trustServerCertificate: builder.TrustServerCertificate,
            encryptionKey: encryptionKey);
    }

    /// <summary>
    /// 读取备份配置（从 backup-config.json 读取）
    /// </summary>
    public static BackupConfig LoadBackupConfig()
    {
        lock (_fileLock)
        {
            var filePath = GetBackupConfigFilePath();
            if (!File.Exists(filePath))
                return new BackupConfig();
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<BackupConfig>(json, _jsonOptions) ?? new BackupConfig();
        }
    }

    /// <summary>
    /// 保存备份配置（写入 backup-config.json）
    /// </summary>
    public static void SaveBackupConfig(BackupConfig config)
    {
        lock (_fileLock)
        {
            var filePath = GetBackupConfigFilePath();
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            var tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, filePath, overwrite: true);
        }
    }

    /// <summary>
    /// 获取 backup-config.json 文件路径
    /// </summary>
    public static string GetBackupConfigFilePath()
        => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup-config.json");

    #region 内部方法

    internal static List<DbConnectionRecord> LoadConnections()
    {
        var filePath = GetFilePath();
        if (!File.Exists(filePath))
            return new List<DbConnectionRecord>();
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<DbConnectionRecord>>(json, _jsonOptions)
               ?? new List<DbConnectionRecord>();
    }

    internal static void SaveConnections(List<DbConnectionRecord> connections)
    {
        var filePath = GetFilePath();
        var json = JsonSerializer.Serialize(connections, _jsonOptions);
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, filePath, overwrite: true);
    }

    internal static string BuildConnectionString(
        string dbType, string? server, int? port, string? database,
        string? username, string? password, string? filePath,
        bool windowsAuth, bool trustServerCert, string? rawConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(rawConnectionString))
            return rawConnectionString;
        if (windowsAuth)
            return $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate={trustServerCert}";
        return $"Server={server};Database={database};User Id={username};Password={password};TrustServerCertificate={trustServerCert}";
    }

    public static string EncryptPassword(string plainText, string? encryptionKey = null)
    {
        var key = GetEncryptionKeyBytes(encryptionKey);
        return SecretProtector.Encrypt(plainText, key);
    }

    public static string DecryptPassword(string cipherText, string? encryptionKey = null)
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

    /// <summary>当前是否仍在使用内置默认加密密钥（未通过 Security:EncryptionKey 覆盖）。用于启动期安全告警。</summary>
    public static bool IsUsingDefaultEncryptionKey(string? encryptionKey)
        => string.IsNullOrWhiteSpace(encryptionKey) || encryptionKey == DefaultEncryptionKey;

    /// <summary>
    /// 获取文件锁对象，供 DbConnectionService 等需要与文件操作同步的场景使用
    /// </summary>
    internal static object FileLock => _fileLock;

    private static byte[] GetEncryptionKeyBytes(string? encryptionKey)
    {
        var keyStr = encryptionKey ?? DefaultEncryptionKey;
        return Encoding.UTF8.GetBytes(keyStr.PadRight(32, '0')[..32]);
    }

    #endregion
}

/// <summary>
/// 数据库连接记录 POCO，与 db-connections.json 中的记录对应。
/// </summary>
public class DbConnectionRecord
{
    public long Id { get; set; }
    public string ConnectionName { get; set; } = "";
    public string DatabaseType { get; set; } = "SqlServer";
    public string? Server { get; set; }
    public int? Port { get; set; }
    public string? DatabaseName { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }  // AES加密后的Base64
    public string? FilePath { get; set; }
    public bool WindowsAuth { get; set; }
    public bool TrustServerCertificate { get; set; } = true;
    public string? ConnectionString { get; set; }
    public string? Description { get; set; }
    public string ConnectionType { get; set; } = "扩展";
    public int Status { get; set; } = 1;
    public DateTime CreatedTime { get; set; } = DateTime.Now;
    public DateTime? UpdatedTime { get; set; }
}
