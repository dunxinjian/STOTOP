using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Services;

public class DbConnectionService : IDbConnectionService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DbConnectionService> _logger;
    private readonly byte[] _encryptionKey;
    private readonly byte[] _encryptionIV;

    public DbConnectionService(
        STOTOPDbContext dbContext,
        IConfiguration configuration,
        ILogger<DbConnectionService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;

        // 从配置读取加密密钥，不存在则使用默认密钥
        var keyStr = _configuration.GetValue<string>("Security:EncryptionKey") ?? "STOTOP@2024!DefaultKey32Bytes!";
        // 确保密钥长度为32字节（AES-256）
        _encryptionKey = Encoding.UTF8.GetBytes(keyStr.PadRight(32, '0')[..32]);
        // 使用密钥前16字节作为IV
        _encryptionIV = _encryptionKey[..16];
    }

    public async Task<List<DbConnectionDto>> GetListAsync()
    {
        var list = await _dbContext.Set<SysDbConnection>()
            .OrderByDescending(e => e.FCreateTime)
            .ToListAsync();

        return list.Select(MapToDto).ToList();
    }

    public async Task<DbConnectionDto> GetByIdAsync(long id)
    {
        var entity = await _dbContext.Set<SysDbConnection>().FindAsync(id);
        if (entity == null)
            throw new Exception($"数据库连接（ID={id}）不存在");

        return MapToDto(entity);
    }

    public async Task<DbConnectionDto> CreateAsync(DbConnectionCreateDto dto)
    {
        // 检查连接名称唯一
        var exists = await _dbContext.Set<SysDbConnection>()
            .AnyAsync(e => e.FConnectionName == dto.Name);
        if (exists)
            throw new Exception($"连接名称 '{dto.Name}' 已存在");

        var entity = new SysDbConnection
        {
            FConnectionName = dto.Name,
            FDatabaseType = dto.DatabaseType,
            FServer = dto.Server,
            FPort = dto.Port,
            FDatabaseName = dto.DatabaseName,
            FUsername = dto.Username,
            FPassword = !string.IsNullOrEmpty(dto.Password) ? EncryptPassword(dto.Password) : null,
            FFilePath = dto.FilePath,
            FWindowsAuth = dto.WindowsAuth ? 1 : 0,
            FTrustServerCertificate = dto.TrustServerCertificate ? 1 : 0,
            FConnectionString = dto.ConnectionString,
            FDescription = dto.Description,
            FStatus = dto.Status,
            FCreateTime = DateTime.Now
        };

        _dbContext.Set<SysDbConnection>().Add(entity);
        await _dbContext.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<DbConnectionDto> UpdateAsync(long id, DbConnectionUpdateDto dto)
    {
        var entity = await _dbContext.Set<SysDbConnection>().FindAsync(id);
        if (entity == null)
            throw new Exception($"数据库连接（ID={id}）不存在");

        // 检查连接名称唯一（排除自身）
        var exists = await _dbContext.Set<SysDbConnection>()
            .AnyAsync(e => e.FConnectionName == dto.Name && e.FID != id);
        if (exists)
            throw new Exception($"连接名称 '{dto.Name}' 已存在");

        entity.FConnectionName = dto.Name;
        entity.FDatabaseType = dto.DatabaseType;
        entity.FServer = dto.Server;
        entity.FPort = dto.Port;
        entity.FDatabaseName = dto.DatabaseName;
        entity.FUsername = dto.Username;
        // 密码为 "******" 表示未修改，保留原密码
        if (!string.IsNullOrEmpty(dto.Password) && dto.Password != "******")
        {
            entity.FPassword = EncryptPassword(dto.Password);
        }
        entity.FFilePath = dto.FilePath;
        entity.FWindowsAuth = dto.WindowsAuth ? 1 : 0;
        entity.FTrustServerCertificate = dto.TrustServerCertificate ? 1 : 0;
        entity.FConnectionString = dto.ConnectionString;
        entity.FDescription = dto.Description;
        entity.FStatus = dto.Status;
        entity.FUpdateTime = DateTime.Now;

        await _dbContext.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task DeleteAsync(long id)
    {
        var entity = await _dbContext.Set<SysDbConnection>().FindAsync(id);
        if (entity == null)
            throw new Exception($"数据库连接（ID={id}）不存在");

        _dbContext.Set<SysDbConnection>().Remove(entity);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> TestConnectionAsync(DbConnectionTestDto dto)
    {
        var connectionString = BuildConnectionString(
            dto.DatabaseType, dto.Server, dto.Port, dto.DatabaseName,
            dto.Username, dto.Password, dto.FilePath,
            dto.WindowsAuth, dto.TrustServerCertificate,
            dto.ConnectionString);

        var dbType = dto.DatabaseType;

        if (dbType.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }

        if (dbType.Equals("MySql", StringComparison.OrdinalIgnoreCase))
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }

        if (dbType.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }

        if (dbType.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }

        throw new Exception($"不支持的数据库类型: {dbType}");
    }

    public async Task<List<DbConnectionOptionDto>> GetOptionsAsync()
    {
        return await _dbContext.Set<SysDbConnection>()
            .Where(e => e.FStatus == 1)
            .OrderBy(e => e.FConnectionName)
            .Select(e => new DbConnectionOptionDto
            {
                Id = e.FID,
                Name = e.FConnectionName
            })
            .ToListAsync();
    }

    public async Task<List<DbTableDto>> GetTablesAsync(long connectionId)
    {
        var entity = await _dbContext.Set<SysDbConnection>().FindAsync(connectionId)
            ?? throw new Exception($"数据库连接（ID={connectionId}）不存在");

        var password = !string.IsNullOrEmpty(entity.FPassword) ? DecryptPassword(entity.FPassword) : null;
        var connectionString = BuildConnectionString(
            entity.FDatabaseType, entity.FServer, entity.FPort, entity.FDatabaseName,
            entity.FUsername, password, entity.FFilePath,
            entity.FWindowsAuth == 1, entity.FTrustServerCertificate == 1,
            entity.FConnectionString);

        var dbType = entity.FDatabaseType;
        var tables = new List<DbTableDto>();

        try
        {
            using var connection = CreateDbConnection(dbType, connectionString);
            await connection.OpenAsync();

            string sql = dbType switch
            {
                var t when t.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT TABLE_NAME AS Name, TABLE_TYPE AS Type FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME",
                var t when t.Equals("MySql", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT TABLE_NAME AS Name, TABLE_TYPE AS Type FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME",
                var t when t.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT table_name AS Name, table_type AS Type FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name",
                var t when t.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT name AS Name, 'TABLE' AS Type FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name",
                _ => throw new Exception($"不支持的数据库类型: {dbType}")
            };

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(new DbTableDto
                {
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    Type = reader["Type"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取表列表失败，连接ID={ConnectionId}", connectionId);
            throw new Exception($"获取表列表失败: {ex.Message}");
        }

        return tables;
    }

    public async Task<List<DbColumnDto>> GetColumnsAsync(long connectionId, string tableName)
    {
        var entity = await _dbContext.Set<SysDbConnection>().FindAsync(connectionId)
            ?? throw new Exception($"数据库连接（ID={connectionId}）不存在");

        var password = !string.IsNullOrEmpty(entity.FPassword) ? DecryptPassword(entity.FPassword) : null;
        var connectionString = BuildConnectionString(
            entity.FDatabaseType, entity.FServer, entity.FPort, entity.FDatabaseName,
            entity.FUsername, password, entity.FFilePath,
            entity.FWindowsAuth == 1, entity.FTrustServerCertificate == 1,
            entity.FConnectionString);

        var dbType = entity.FDatabaseType;
        var columns = new List<DbColumnDto>();

        try
        {
            using var connection = CreateDbConnection(dbType, connectionString);
            await connection.OpenAsync();

            if (dbType.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                // Sqlite PRAGMA 不支持参数化，需安全校验表名
                if (!Regex.IsMatch(tableName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                    throw new Exception("无效的表名");

                using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA table_info({tableName})";
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    columns.Add(new DbColumnDto
                    {
                        Name = reader["name"]?.ToString() ?? string.Empty,
                        DataType = reader["type"]?.ToString() ?? string.Empty,
                        IsNullable = reader["notnull"]?.ToString() == "0",
                        IsPrimaryKey = reader["pk"]?.ToString() == "1",
                        MaxLength = null
                    });
                }
            }
            else
            {
                string sql = dbType switch
                {
                    var t when t.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) =>
                        @"SELECT 
                            c.COLUMN_NAME AS Name,
                            c.DATA_TYPE AS DataType,
                            CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IsNullable,
                            CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,
                            c.CHARACTER_MAXIMUM_LENGTH AS MaxLength
                        FROM INFORMATION_SCHEMA.COLUMNS c
                        LEFT JOIN (
                            SELECT ku.COLUMN_NAME
                            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.TABLE_NAME = @tableName
                        ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
                        WHERE c.TABLE_NAME = @tableName
                        ORDER BY c.ORDINAL_POSITION",
                    var t when t.Equals("MySql", StringComparison.OrdinalIgnoreCase) =>
                        @"SELECT 
                            COLUMN_NAME AS Name,
                            DATA_TYPE AS DataType,
                            CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IsNullable,
                            CASE WHEN COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END AS IsPrimaryKey,
                            CHARACTER_MAXIMUM_LENGTH AS MaxLength
                        FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName
                        ORDER BY ORDINAL_POSITION",
                    var t when t.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) =>
                        @"SELECT 
                            c.column_name AS Name,
                            c.data_type AS DataType,
                            CASE WHEN c.is_nullable = 'YES' THEN true ELSE false END AS IsNullable,
                            CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END AS IsPrimaryKey,
                            c.character_maximum_length AS MaxLength
                        FROM information_schema.columns c
                        LEFT JOIN (
                            SELECT kcu.column_name
                            FROM information_schema.table_constraints tc
                            JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
                            WHERE tc.constraint_type = 'PRIMARY KEY' AND tc.table_name = @tableName
                        ) pk ON c.column_name = pk.column_name
                        WHERE c.table_schema = 'public' AND c.table_name = @tableName
                        ORDER BY c.ordinal_position",
                    _ => throw new Exception($"不支持的数据库类型: {dbType}")
                };

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                var param = command.CreateParameter();
                param.ParameterName = "@tableName";
                param.Value = tableName;
                command.Parameters.Add(param);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    columns.Add(new DbColumnDto
                    {
                        Name = reader["Name"]?.ToString() ?? string.Empty,
                        DataType = reader["DataType"]?.ToString() ?? string.Empty,
                        IsNullable = Convert.ToBoolean(reader["IsNullable"]),
                        IsPrimaryKey = Convert.ToBoolean(reader["IsPrimaryKey"]),
                        MaxLength = reader["MaxLength"] == DBNull.Value ? null : Convert.ToInt32(reader["MaxLength"])
                    });
                }
            }
        }
        catch (Exception ex) when (ex is not Exception { Message: "无效的表名" })
        {
            _logger.LogError(ex, "获取字段列表失败，连接ID={ConnectionId}，表名={TableName}", connectionId, tableName);
            throw new Exception($"获取字段列表失败: {ex.Message}");
        }

        return columns;
    }

    /// <summary>
    /// 根据连接ID获取已解密的完整连接字符串。
    /// 如果 F连接字符串 字段有值，优先使用；否则用各字段拼接（密码自动解密）。
    /// </summary>
    public async Task<string> GetConnectionStringAsync(long connectionId)
    {
        var entity = await _dbContext.Set<SysDbConnection>().FindAsync(connectionId)
            ?? throw new Exception($"数据库连接（ID={connectionId}）不存在或已禁用");

        if (entity.FStatus != 1)
            throw new Exception($"数据库连接（ID={connectionId}）已被禁用");

        var password = !string.IsNullOrEmpty(entity.FPassword) ? DecryptPassword(entity.FPassword) : null;
        return BuildConnectionString(
            entity.FDatabaseType, entity.FServer, entity.FPort, entity.FDatabaseName,
            entity.FUsername, password, entity.FFilePath,
            entity.FWindowsAuth == 1, entity.FTrustServerCertificate == 1,
            entity.FConnectionString);
    }

    public async Task<bool> HasSystemConnectionAsync()
    {
        return await _dbContext.Set<SysDbConnection>()
            .AnyAsync(e => e.FStatus == 1);
    }

    #region 私有方法

    private static DbConnection CreateDbConnection(string dbType, string connectionString)
    {
        if (dbType.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            return new SqlConnection(connectionString);
        if (dbType.Equals("MySql", StringComparison.OrdinalIgnoreCase))
            return new MySqlConnection(connectionString);
        if (dbType.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
            return new NpgsqlConnection(connectionString);
        if (dbType.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            return new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        throw new Exception($"不支持的数据库类型: {dbType}");
    }

    private DbConnectionDto MapToDto(SysDbConnection entity)
    {
        return new DbConnectionDto
        {
            Id = entity.FID,
            Name = entity.FConnectionName,
            DatabaseType = entity.FDatabaseType,
            Server = entity.FServer,
            Port = entity.FPort,
            DatabaseName = entity.FDatabaseName,
            Username = entity.FUsername,
            Password = "******", // 密码脱敏
            FilePath = entity.FFilePath,
            WindowsAuth = entity.FWindowsAuth == 1,
            TrustServerCertificate = entity.FTrustServerCertificate == 1,
            ConnectionString = entity.FConnectionString,
            Description = entity.FDescription,
            Status = entity.FStatus,
            CreatedTime = entity.FCreateTime,
            UpdatedTime = entity.FUpdateTime
        };
    }

    private static string BuildConnectionString(
        string dbType, string? server, int? port, string? database,
        string? username, string? password, string? filePath,
        bool windowsAuth, bool trustServerCert, string? rawConnectionString)
    {
        // 如果提供了完整连接字符串，优先使用
        if (!string.IsNullOrWhiteSpace(rawConnectionString))
            return rawConnectionString;

        if (dbType.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            if (windowsAuth)
                return $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate={trustServerCert}";
            return $"Server={server};Database={database};User Id={username};Password={password};TrustServerCertificate={trustServerCert}";
        }

        if (dbType.Equals("MySql", StringComparison.OrdinalIgnoreCase))
        {
            return $"Server={server};Port={port ?? 3306};Database={database};Uid={username};Pwd={password}";
        }

        if (dbType.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            return $"Host={server};Port={port ?? 5432};Database={database};Username={username};Password={password}";
        }

        if (dbType.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return $"Data Source={filePath}";
        }

        throw new Exception($"不支持的数据库类型: {dbType}");
    }

    private string EncryptPassword(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = _encryptionIV;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(encryptedBytes);
    }

    private string DecryptPassword(string cipherText)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = _encryptionIV;

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(cipherText);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    #endregion
}
