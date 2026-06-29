using System.Security.Cryptography;
using System.Text;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

[ApiController]
[Route("api/system/database")]
[Authorize]
public class DatabaseController : ControllerBase
{
    private readonly IDatabaseService _databaseService;
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<DatabaseController> _logger;
    private readonly IHubContext<DatabaseProgressHub> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _env;
    private readonly IAdminAuthorizationService _adminAuth;

    public DatabaseController(
        IDatabaseService databaseService,
        STOTOPDbContext dbContext,
        ILogger<DatabaseController> logger,
        IHubContext<DatabaseProgressHub> hubContext,
        IConfiguration configuration,
        IHostEnvironment env,
        IAdminAuthorizationService adminAuth)
    {
        _databaseService = databaseService;
        _dbContext = dbContext;
        _logger = logger;
        _hubContext = hubContext;
        _configuration = configuration;
        _env = env;
        _adminAuth = adminAuth;
    }

    /// <summary>
    /// 系统已初始化后，破坏性/重建类操作必须由管理员执行：
    /// 仅"已认证"不足以授权（否则 setup token 或任意普通用户即可触发重建库）。
    /// 系统未初始化时返回 null（首次安装的匿名引导阶段）。
    /// </summary>
    private IActionResult? EnsureAdminWhenInitialized()
    {
        if (!DbConnectionsHelper.IsInitialized())
            return null;

        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { success = false, message = "系统已初始化，此操作需要管理员登录" });

        if (!_adminAuth.IsAdmin(User))
            return StatusCode(403, new { success = false, message = "此操作需要管理员权限" });

        return null;
    }

    /// <summary>
    /// 获取数据库状态
    /// </summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<ApiResult<DatabaseStatusResult>> GetStatus()
    {
        var result = await _databaseService.GetDatabaseStatusAsync();
        return ApiResult<DatabaseStatusResult>.Success(result);
    }

    /// <summary>
    /// 测试数据库连接
    /// </summary>
    [HttpPost("test-connection")]
    [AllowAnonymous]
    public async Task<ApiResult<TestConnectionResult>> TestConnection([FromBody] DatabaseConfigRequest request)
    {
        var provider = request.DatabaseType ?? "SqlServer";
        var connectionString = BuildConnectionString(request);
        var result = await _databaseService.TestConnectionAsync(provider, connectionString);
        return ApiResult<TestConnectionResult>.Success(result);
    }

    /// <summary>
    /// 执行数据库初始化
    /// </summary>
    [HttpPost("initialize")]
    [AllowAnonymous]
    public async Task<IActionResult> Initialize([FromBody] DatabaseConfigRequest? request)
    {
        var guard = EnsureAdminWhenInitialized();
        if (guard != null) return guard;

        var provider = request?.DatabaseType;
        var connectionString = request != null ? BuildConnectionString(request) : null;
        var result = await _databaseService.InitializeDatabaseAsync(provider, connectionString);
        
        if (result.Success)
        {
            return Ok(ApiResult<InitializeResult>.Success(result));
        }
        else
        {
            return Ok(ApiResult<InitializeResult>.Fail(result.Message));
        }
    }

    /// <summary>
    /// 获取当前数据库配置
    /// </summary>
    [HttpGet("config")]
    [Authorize]
    public async Task<ApiResult<DatabaseConfigResult>> GetConfig()
    {
        var result = await _databaseService.GetConfigAsync();
        return ApiResult<DatabaseConfigResult>.Success(result);
    }

    /// <summary>
    /// 更新数据库配置
    /// </summary>
    [HttpPut("config")]
    [Authorize]
    public async Task<ApiResult<UpdateConfigResult>> UpdateConfig([FromBody] UpdateConfigRequest request)
    {
        var result = await _databaseService.UpdateConfigAsync(request.Provider, request.ConnectionString);
        
        if (result.Success)
        {
            return ApiResult<UpdateConfigResult>.Success(result);
        }
        else
        {
            return ApiResult<UpdateConfigResult>.Fail(result.Message);
        }
    }

    /// <summary>
    /// 分析数据库表状态
    /// </summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeDatabase([FromBody] ConnectionStringRequest request)
    {
        var guard = EnsureAdminWhenInitialized();
        if (guard != null) return guard;

        try
        {
            var connectionString = request.ConnectionString;
            if (request.ConnectionId.HasValue)
            {
                connectionString = DbConnectionsHelper.GetConnectionStringById(request.ConnectionId.Value);
                if (string.IsNullOrEmpty(connectionString))
                    return BadRequest(new { success = false, message = "未找到指定的数据库连接" });
            }

            var result = await _databaseService.AnalyzeDatabaseAsync(connectionString!);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "分析数据库失败");
            return StatusCode(500, new { success = false, message = $"分析失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 全新初始化数据库
    /// </summary>
    [HttpPost("full-initialize")]
    [AllowAnonymous]
    public async Task<IActionResult> FullInitialize([FromBody] ConnectionStringRequest request)
    {
        var guard = EnsureAdminWhenInitialized();
        if (guard != null) return guard;

        try
        {
            var connectionString = request.ConnectionString;
            if (request.ConnectionId.HasValue)
            {
                connectionString = DbConnectionsHelper.GetConnectionStringById(request.ConnectionId.Value);
                if (string.IsNullOrEmpty(connectionString))
                    return BadRequest(new { success = false, message = "未找到指定的数据库连接" });
            }

            await _databaseService.FullInitializeAsync(connectionString!);
            return Ok(new { success = true, message = "数据库全新初始化完成" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "全新初始化失败");
            return StatusCode(500, new { success = false, message = $"全新初始化失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 保留初始化数据库（带 SignalR 实时进度推送）
    /// </summary>
    [HttpPost("preserve-initialize")]
    [AllowAnonymous]
    public async Task<IActionResult> PreserveInitialize([FromBody] PreserveInitializeRequest request)
    {
        var guard = EnsureAdminWhenInitialized();
        if (guard != null) return guard;

        // 从请求头获取 SignalR connectionId
        var signalRConnectionId = Request.Headers["X-SignalR-ConnectionId"].FirstOrDefault();

        // 构建进度回调
        Func<string, string, int, int, Task>? onProgress = null;
        if (!string.IsNullOrWhiteSpace(signalRConnectionId))
        {
            onProgress = async (stepName, status, currentStep, totalSteps) =>
            {
                try
                {
                    await _hubContext.Clients.Client(signalRConnectionId).SendAsync(
                        "OnPreserveProgress",
                        new { stepName, status, currentStep, totalSteps });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "推送 SignalR 进度失败: {StepName} {Status}", stepName, status);
                }
            };
        }

        try
        {
            var connectionString = request.ConnectionString;
            if (request.ConnectionId.HasValue)
            {
                connectionString = DbConnectionsHelper.GetConnectionStringById(request.ConnectionId.Value);
                if (string.IsNullOrEmpty(connectionString))
                    return BadRequest(new { success = false, message = "未找到指定的数据库连接" });
            }

            await _databaseService.PreserveInitializeAsync(
                connectionString!,
                request.TableActions,
                request.BackupPath,
                onProgress);
            return Ok(new { success = true, message = "数据库保留初始化完成" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保留初始化失败");
            // 推送错误进度
            if (onProgress != null)
            {
                try
                {
                    await _hubContext.Clients.Client(signalRConnectionId!).SendAsync(
                        "OnPreserveProgress",
                        new { stepName = "初始化", status = "error", currentStep = 0, totalSteps = 0, error = ex.Message });
                }
                catch { /* 忽略推送失败 */ }
            }
            return StatusCode(500, new { success = false, message = $"保留初始化失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 保留初始化 dry-run 预览
    /// </summary>
    [HttpPost("preserve-initialize/dry-run")]
    [AllowAnonymous]
    public async Task<IActionResult> PreserveDryRun([FromBody] PreserveInitializeRequest request)
    {
        var guard = EnsureAdminWhenInitialized();
        if (guard != null) return guard;

        try
        {
            var connectionString = request.ConnectionString;
            if (request.ConnectionId.HasValue)
            {
                connectionString = DbConnectionsHelper.GetConnectionStringById(request.ConnectionId.Value);
                if (string.IsNullOrEmpty(connectionString))
                    return BadRequest(new { success = false, message = "未找到指定的数据库连接" });
            }

            var result = await _databaseService.DryRunAsync(connectionString!, request.TableActions);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "dry-run 预览失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 查询 SQL Server 默认备份目录
    /// </summary>
    [HttpPost("backup-directory")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDefaultBackupDirectory([FromBody] ConnectionStringRequest request)
    {
        try
        {
            var connectionString = request.ConnectionString;
            if (request.ConnectionId.HasValue)
            {
                connectionString = DbConnectionsHelper.GetConnectionStringById(request.ConnectionId.Value);
                if (string.IsNullOrEmpty(connectionString))
                    return Ok(new { success = true, data = new { backupDirectory = "" }, warning = "未找到指定的数据库连接" });
            }

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"EXEC master.dbo.xp_instance_regread 
                N'HKEY_LOCAL_MACHINE', 
                N'SOFTWARE\Microsoft\MSSQLServer\MSSQLServer', 
                N'BackupDirectory'";
            using var reader = await cmd.ExecuteReaderAsync();
            string? backupDir = null;
            if (await reader.ReadAsync())
                backupDir = reader.GetString(reader.GetOrdinal("Data"));
            return Ok(new { success = true, data = new { backupDirectory = backupDir ?? "" } });
        }
        catch (Exception ex)
        {
            return Ok(new { success = true, data = new { backupDirectory = "" }, warning = ex.Message });
        }
    }

    /// <summary>
    /// 获取自动备份配置
    /// </summary>
    [HttpGet("backup-config")]
    public IActionResult GetBackupConfig()
    {
        var config = DbConnectionsHelper.LoadBackupConfig();
        return Ok(new { success = true, data = config });
    }

    /// <summary>
    /// 保存自动备份配置，并更新 Hangfire RecurringJob
    /// </summary>
    [HttpPost("backup-config")]
    public IActionResult SaveBackupConfig([FromBody] BackupConfig config)
    {
        try
        {
            DbConnectionsHelper.SaveBackupConfig(config);
            UpdateBackupSchedule(config);
            return Ok(new { success = true, message = "备份配置已保存" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存备份配置失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Setup 口令认证，验证通过后生成 admin JWT token
    /// </summary>
    [AllowAnonymous]
    [HttpPost("setup-auth")]
    public IActionResult SetupAuth([FromBody] SetupAuthRequest request)
    {
        try
        {
            // 系统已初始化后，禁止再通过初始化口令换取 token（避免 setup token 被用于触发破坏性端点）。
            if (DbConnectionsHelper.IsInitialized())
            {
                _logger.LogWarning("Setup 口令认证被拒绝：系统已初始化");
                return Ok(ApiResult<object>.Fail("系统已初始化，初始化口令认证不可用，请使用管理员账号登录"));
            }

            // 口令来自配置 Setup:Passphrase（生产应通过环境变量 Setup__Passphrase 提供）；
            // 仅开发环境在未配置时回退到固定占位口令，便于本地初始化。
            var expected = _configuration["Setup:Passphrase"];
            if (string.IsNullOrWhiteSpace(expected))
            {
                if (_env.IsDevelopment())
                {
                    expected = "SystemSetup";
                    _logger.LogWarning("Setup:Passphrase 未配置，开发环境回退到默认初始化口令，切勿用于生产");
                }
                else
                {
                    _logger.LogError("Setup:Passphrase 未配置，拒绝 Setup 认证（生产环境必须配置初始化口令）");
                    return Ok(ApiResult<object>.Fail("初始化口令未配置，无法进行 Setup 认证"));
                }
            }

            var provided = request.Passphrase ?? string.Empty;
            var matched = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(provided),
                Encoding.UTF8.GetBytes(expected));
            if (!matched)
            {
                _logger.LogWarning("Setup 口令认证失败：口令错误");
                return Ok(ApiResult<object>.Fail("口令错误"));
            }

            var token = _databaseService.GenerateSetupToken();
            _logger.LogInformation("Setup 口令认证成功，已生成临时 Token");

            return Ok(ApiResult<SetupAuthResponse>.Success(new SetupAuthResponse
            {
                Token = token,
                TokenType = "Bearer",
                ExpiresIn = 3600
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Setup 口令认证过程中发生异常");
            return StatusCode(500, ApiResult<object>.Fail("认证服务异常，请检查服务器日志", 500));
        }
    }

    /// <summary>
    /// 注册或移除 Hangfire 定时备份任务
    /// </summary>
    private static void UpdateBackupSchedule(BackupConfig config)
    {
        if (config.Enabled && !string.IsNullOrWhiteSpace(config.CronExpression))
        {
            RecurringJob.AddOrUpdate<DatabaseService>(
                "database-auto-backup",
                service => service.ExecuteScheduledBackupAsync(),
                config.CronExpression);
        }
        else
        {
            RecurringJob.RemoveIfExists("database-auto-backup");
        }
    }

    /// <summary>
    /// 根据前端传入的配置字段构建连接字符串
    /// </summary>
    private static string BuildConnectionString(DatabaseConfigRequest request)
    {
        // 如果前端直接传了 connectionString，优先使用
        if (!string.IsNullOrWhiteSpace(request.ConnectionString))
            return request.ConnectionString;

        if (request.UseWindowsAuth == true)
        {
            return $"Server={request.Server};Database={request.Database};Trusted_Connection=True;TrustServerCertificate={request.TrustServerCertificate ?? true}";
        }
        return $"Server={request.Server};Database={request.Database};User Id={request.Username};Password={request.Password};TrustServerCertificate={request.TrustServerCertificate ?? true}";
    }
}

/// <summary>
/// 数据库初始化进度推送 Hub（无需认证，保留初始化前系统可能未登录）
/// </summary>
public class DatabaseProgressHub : Microsoft.AspNetCore.SignalR.Hub
{
}

public class DatabaseConfigRequest
{
    public string? DatabaseType { get; set; }
    public string? ConnectionString { get; set; }
    public string? Server { get; set; }
    public string? Database { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool? UseWindowsAuth { get; set; }
    public bool? TrustServerCertificate { get; set; }
    public string? FilePath { get; set; }
}

public class UpdateConfigRequest
{
    public string Provider { get; set; } = "";
    public string ConnectionString { get; set; } = "";
}
