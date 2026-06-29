using System.Text.Json.Serialization;

namespace STOTOP.Module.System.Dtos;

/// <summary>
/// 数据库连接列表/详情返回 DTO
/// </summary>
public class DbConnectionDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("databaseType")]
    public string DatabaseType { get; set; } = string.Empty;
    [JsonPropertyName("server")]
    public string? Server { get; set; }
    [JsonPropertyName("port")]
    public int? Port { get; set; }
    [JsonPropertyName("databaseName")]
    public string? DatabaseName { get; set; }
    [JsonPropertyName("username")]
    public string? Username { get; set; }
    [JsonPropertyName("password")]
    public string Password { get; set; } = "******";
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
    [JsonPropertyName("windowsAuth")]
    public bool WindowsAuth { get; set; }
    [JsonPropertyName("trustServerCertificate")]
    public bool TrustServerCertificate { get; set; }
    [JsonPropertyName("connectionString")]
    public string? ConnectionString { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("connectionType")]
    public string ConnectionType { get; set; } = "扩展";
    [JsonPropertyName("status")]
    public int Status { get; set; }
    [JsonPropertyName("createdTime")]
    public DateTime CreatedTime { get; set; }
    [JsonPropertyName("updatedTime")]
    public DateTime? UpdatedTime { get; set; }
}

/// <summary>
/// 创建数据库连接请求 DTO
/// </summary>
public class DbConnectionCreateDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("databaseType")]
    public string DatabaseType { get; set; } = string.Empty;
    [JsonPropertyName("server")]
    public string? Server { get; set; }
    [JsonPropertyName("port")]
    public int? Port { get; set; }
    [JsonPropertyName("databaseName")]
    public string? DatabaseName { get; set; }
    [JsonPropertyName("username")]
    public string? Username { get; set; }
    [JsonPropertyName("password")]
    public string? Password { get; set; }
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
    [JsonPropertyName("windowsAuth")]
    public bool WindowsAuth { get; set; } = false;
    [JsonPropertyName("trustServerCertificate")]
    public bool TrustServerCertificate { get; set; } = false;
    [JsonPropertyName("connectionString")]
    public string? ConnectionString { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("connectionType")]
    public string? ConnectionType { get; set; }
    [JsonPropertyName("status")]
    public int Status { get; set; } = 1;
}

/// <summary>
/// 更新数据库连接请求 DTO
/// </summary>
public class DbConnectionUpdateDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("databaseType")]
    public string DatabaseType { get; set; } = string.Empty;
    [JsonPropertyName("server")]
    public string? Server { get; set; }
    [JsonPropertyName("port")]
    public int? Port { get; set; }
    [JsonPropertyName("databaseName")]
    public string? DatabaseName { get; set; }
    [JsonPropertyName("username")]
    public string? Username { get; set; }
    [JsonPropertyName("password")]
    public string? Password { get; set; }
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
    [JsonPropertyName("windowsAuth")]
    public bool WindowsAuth { get; set; } = false;
    [JsonPropertyName("trustServerCertificate")]
    public bool TrustServerCertificate { get; set; } = false;
    [JsonPropertyName("connectionString")]
    public string? ConnectionString { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("status")]
    public int Status { get; set; } = 1;
}

/// <summary>
/// 测试数据库连接请求 DTO（不保存，仅测试）
/// </summary>
public class DbConnectionTestDto
{
    [JsonPropertyName("databaseType")]
    public string DatabaseType { get; set; } = string.Empty;
    [JsonPropertyName("server")]
    public string? Server { get; set; }
    [JsonPropertyName("port")]
    public int? Port { get; set; }
    [JsonPropertyName("databaseName")]
    public string? DatabaseName { get; set; }
    [JsonPropertyName("username")]
    public string? Username { get; set; }
    [JsonPropertyName("password")]
    public string? Password { get; set; }
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
    [JsonPropertyName("windowsAuth")]
    public bool WindowsAuth { get; set; } = false;
    [JsonPropertyName("trustServerCertificate")]
    public bool TrustServerCertificate { get; set; } = false;
    [JsonPropertyName("connectionString")]
    public string? ConnectionString { get; set; }
}

/// <summary>
/// 数据库连接下拉选项 DTO（仅 Id + Name）
/// </summary>
public class DbConnectionOptionDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 数据库表 DTO
/// </summary>
public class DbTableDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("type")]
    public string? Type { get; set; }  // TABLE, VIEW
}

/// <summary>
/// 数据库列/字段 DTO
/// </summary>
public class DbColumnDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = string.Empty;
    [JsonPropertyName("isNullable")]
    public bool IsNullable { get; set; }
    [JsonPropertyName("isPrimaryKey")]
    public bool IsPrimaryKey { get; set; }
    [JsonPropertyName("maxLength")]
    public int? MaxLength { get; set; }
}
