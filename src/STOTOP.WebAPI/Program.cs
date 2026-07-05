using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using STOTOP.WebAPI.Filters;
using Microsoft.OpenApi.Models;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Infrastructure.Middleware;
using STOTOP.Infrastructure.Repositories;
using STOTOP.WebAPI.Data;
using STOTOP.WebAPI.Data.Seeders;
using STOTOP.WebAPI.Hubs;
using STOTOP.WebAPI.Services;
using STOTOP.Module.System;
using STOTOP.Module.System.Controllers;
using STOTOP.Module.System.Services;
using STOTOP.Module.System.Services.Interfaces;
using STOTOP.Module.Finance;
using STOTOP.Module.Supplier;
using STOTOP.Module.HR;
using STOTOP.Module.Dormitory;
using STOTOP.Module.Vehicle;
using STOTOP.Module.Insurance;
using STOTOP.Module.Express;
using STOTOP.Module.Points;
using STOTOP.Module.KSF;
using STOTOP.Module.PPV;
using STOTOP.Module.Salary;
using STOTOP.Module.Task;
using STOTOP.Module.Task.Hubs;
using STOTOP.Module.CRM;
using STOTOP.Module.Contract;
using STOTOP.Module.Quality;
using STOTOP.Module.Express.Services.Agents;
using STOTOP.Module.Express.Services.Billing;
using STOTOP.Module.Conference;
using STOTOP.Module.Workflow;
using STOTOP.Module.CardFlow;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Hubs;
using STOTOP.Module.System.Middleware;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using STOTOP.Module.Contract.Jobs;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.Workflow.Jobs;
using STOTOP.WebAPI.Jobs;
using Hangfire;
using Hangfire.SqlServer;
using Asp.Versioning;
using System.Threading.RateLimiting;

var earlyExportBaselineIndex = Array.FindIndex(args, arg => string.Equals(arg, "--export-baseline", StringComparison.OrdinalIgnoreCase));
if (earlyExportBaselineIndex >= 0)
{
    var outputPath = earlyExportBaselineIndex + 1 < args.Length
        ? args[earlyExportBaselineIndex + 1]
        : Path.Combine(Directory.GetCurrentDirectory(), "Temp", "baseline-snapshot.json");
    var baselineConnectionString = DbConnectionsHelper.GetSystemConnectionString();
    BaselineSnapshotExporter.Export(
        baselineConnectionString ?? throw new InvalidOperationException("系统数据库连接未配置"),
        outputPath);
    Console.WriteLine($"已导出数据库 baseline 快照: {outputPath}");
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Global unhandled exception protection - prevent process crash from background task failures
AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
{
    var ex = args.ExceptionObject as Exception;
    Console.Error.WriteLine($"[FATAL] Unhandled exception caught: {ex?.GetType().Name} - {ex?.Message}");
    Console.Error.WriteLine($"[FATAL] Stack trace: {ex?.StackTrace}");
    // Log but don't crash - allows graceful degradation
    if (args.IsTerminating)
    {
        Console.Error.WriteLine("[FATAL] Process is terminating, attempting graceful shutdown...");
    }
};

// ========== 安全：JWT 签名密钥校验 ==========
// 生产环境必须通过环境变量 Jwt__Secret（或用户机密/密钥库）提供强随机密钥；
// 开发环境在未配置/仍为占位符时回退到固定的"仅开发"密钥，避免本地启动失败。
// 解析结果写回配置，使 AuthService / DatabaseService 等读取 Jwt:Secret 的位置获得同一值。
{
    const string jwtPlaceholder = "YourSuperSecretKeyAtLeast32Characters!";
    var configuredSecret = builder.Configuration["Jwt:Secret"];
    if (string.IsNullOrWhiteSpace(configuredSecret) || configuredSecret == jwtPlaceholder)
    {
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration["Jwt:Secret"] = "DEV-ONLY-INSECURE-JWT-SECRET-DO-NOT-USE-IN-PROD";
            Console.WriteLine("[WARN] 未配置有效的 Jwt:Secret，开发环境已回退到默认密钥，切勿用于生产。");
        }
        else
        {
            throw new InvalidOperationException(
                "Jwt:Secret 未配置或仍为占位符。生产环境必须通过环境变量 Jwt__Secret 提供至少 32 字符的强随机密钥。");
        }
    }
}

// Add services to the container.
builder.Services.AddControllers()
    .ConfigureApplicationPartManager(manager =>
    {
        var retiredOaParts = manager.ApplicationParts
            .Where(part => string.Equals(part.Name, "STOTOP.Module.OA", StringComparison.Ordinal))
            .ToList();

        foreach (var part in retiredOaParts)
        {
            manager.ApplicationParts.Remove(part);
        }
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// DbContext - 仅支持 SQL Server
var connectionString = DbConnectionsHelper.GetSystemConnectionString(builder.Configuration.GetValue<string>("Security:EncryptionKey"));

builder.Services.AddDbContext<STOTOPDbContext>(options =>
{
    options.UseSqlServer(connectionString, opts => opts.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(10),
        errorNumbersToAdd: null));
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);
}, ServiceLifetime.Scoped);

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.FromMinutes(
            builder.Configuration.GetValue<int?>("Jwt:ClockSkewMinutes") ?? 5)
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/hubs") || path.StartsWithSegments("/hangfire")))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Authorization policies
builder.Services.AddAuthorization();

// Swagger with JWT Bearer and API Versioning
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "STOTOP API", Version = "v1", Description = "API v1.0" });
    c.OperationFilter<FileUploadOperationFilter>();
    
    // 解决不同命名空间下同名类的 SchemaId 冲突问题，同时处理泛型类型的友好名称
    c.CustomSchemaIds(type =>
    {
        if (!type.IsGenericType)
            return (type.FullName ?? type.Name).Replace("+", ".");

        // 递归构建泛型类型的友好名称
        static string GetFriendlyName(Type t)
        {
            if (!t.IsGenericType)
                return (t.FullName ?? t.Name).Replace("+", ".");

            var baseName = t.Name;
            var tickIndex = baseName.IndexOf('`');
            if (tickIndex > 0)
                baseName = baseName[..tickIndex];

            var args = t.GetGenericArguments();
            return baseName + "Of" + string.Join("And", args.Select(GetFriendlyName));
        }

        var ns = type.Namespace;
        var friendlyName = GetFriendlyName(type);
        return string.IsNullOrEmpty(ns) ? friendlyName : $"{ns}.{friendlyName}";
    });
    
    // JWT Bearer configuration
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version")
    );
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Rate Limiting - 为登录端点添加限流保护
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,        // 最多 10 次尝试
                Window = TimeSpan.FromMinutes(5),  // 每 5 分钟窗口
                QueueLimit = 0,
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        var message = "请求过于频繁，请稍后再试";
        var json = System.Text.Json.JsonSerializer.Serialize(new { code = 429, message });
        context.HttpContext.Response.ContentType = "application/json";
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync(json, cancellationToken: token);
    };
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<STOTOPDbContext>("database");

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:9001" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("X-New-Token", "X-Token-Expires-In");
    });
});

// Repository
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Event Dispatcher
builder.Services.AddSingleton<IEventDispatcher, InProcessEventDispatcher>();

// Database Seeder
builder.Services.AddSingleton<IDatabaseSeeder, DatabaseMigrator>();

// Dynamic DbContext Factory - 用于运行时切换数据库提供者
builder.Services.AddSingleton<IDynamicDbContextFactory, DynamicDbContextFactory>();

// Org Context
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IOrgContextAccessor, HttpOrgContextAccessor>();
// 受控平台作用域工厂：与 IOrgContextAccessor 同 Scoped，共享实例——Enter 置位后本作用域 DbContext 立即放行租户硬墙。
builder.Services.AddScoped<IPlatformScopeFactory, STOTOP.Module.System.Services.PlatformScopeFactory>();
builder.Services.AddSingleton<STOTOP.Core.Services.ITenantResolver, STOTOP.Module.System.Services.TenantResolver>();
// 租户级作用域工厂（只设 CurrentTenantId、不放行全库）+ per-tenant 后台迭代地基（阶段4 收尾：后台 Job 逐活跃租户处理）。
builder.Services.AddScoped<STOTOP.Core.Services.ITenantScopeFactory, STOTOP.Module.System.Services.TenantScopeFactory>();
builder.Services.AddScoped<STOTOP.Core.Services.ITenantIterationService, STOTOP.Module.System.Services.TenantIterationService>();

// System Module
builder.Services.AddSystemModule();

// Finance Module
builder.Services.AddFinanceModule();

// Supplier Module
builder.Services.AddSupplierModule();

// HR Module
builder.Services.AddHrModule();

// Dormitory Module
builder.Services.AddDormitoryModule();

// Vehicle Module
builder.Services.AddVehicleModule();

// Insurance Module
builder.Services.AddInsuranceModule();

// CardFlow Module (must be before Express - Express depends on IImportService/IAutoPluginProgressReporter)
// 包含原 DataCenter + CFAutoPlugin 的所有服务注册
builder.Services.AddCardFlowModule(builder.Configuration);

// Express Module
builder.Services.AddExpressModule();

builder.Services.AddScoped<PricingPlugin>();
builder.Services.AddScoped<CostPlugin>();

// Points Module
builder.Services.AddPointsModule();

// KSF Module
builder.Services.AddKsfModule();

// PPV Module
builder.Services.AddPpvModule();

// Salary Module
builder.Services.AddSalaryModule();

// Task Module
builder.Services.AddTaskModule();

// CRM Module
builder.Services.AddCrmModule();

// Contract Module
builder.Services.AddContractModule();

// Quality Module
builder.Services.AddQualityModule();

// Conference Module
builder.Services.AddConferenceModule();

// Workflow Module
builder.Services.AddWorkflowModule();

// WorkHub Service
builder.Services.AddScoped<IWorkHubService, WorkHubService>();
builder.Services.AddScoped<IWorkHubNotifier, WorkHubNotifier>();

// Jobs
builder.Services.AddScoped<QualityOverdueCheckJob>();
builder.Services.AddScoped<ShentongUnificationJob>();

// 钉钉群机器人 Webhook 推送（AlertBotJob / DailyReportBotJob / WeeklyReportBotJob / BotPushController 依赖）
builder.Services.AddSingleton<DingTalkBotService>();

// SignalR
builder.Services.AddSignalR();

// Response Compression - 压缩API响应减少传输体积
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults
        .MimeTypes.Concat(new[] { "application/json" });
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});
// SignalR 进度通知（统一注册，替代原 DataCenter + CFAutoPlugin 三模块桥接）
builder.Services.AddSingleton<SignalRProgressNotifier>();
builder.Services.AddSingleton<AutoPluginProgressReporter>();
builder.Services.AddSingleton<IProgressNotifier>(sp => sp.GetRequiredService<SignalRProgressNotifier>());
builder.Services.AddSingleton<IAutoPluginProgressReporter>(sp => sp.GetRequiredService<AutoPluginProgressReporter>());
builder.Services.AddSingleton<IDingTalkSyncProgressNotifier>(sp => sp.GetRequiredService<SignalRProgressNotifier>());

// Hangfire
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        SchemaName = "hangfire",
        PrepareSchemaIfNecessary = true
    }));
builder.Services.AddHangfireServer();

// 在 Build 之前静态注册所有模块配置程序集，确保 EF Core 模型构建时一定能发现所有实体配置
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.System"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.Finance"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.Supplier"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.HR"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.Dormitory"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.Vehicle"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.Express"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.Points"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.Task"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.CRM"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.Contract"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.Quality"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.Conference"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.Insurance"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.Workflow"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.CardFlow"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.KSF"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.PPV"));
STOTOPDbContext.RegisterModuleAssembly(Assembly.Load("STOTOP.Module.Salary"));

var app = builder.Build();

// 注册 Express 模块的 Agent 插件到 AutoPluginFactory
var pluginFactory = app.Services.GetRequiredService<AutoPluginFactory>();
pluginFactory.Register<PricingPlugin>("Pricing");
pluginFactory.Register<CostPlugin>("Cost");

var runDatabaseInitialization = args.Contains("--init-database", StringComparer.OrdinalIgnoreCase);
var runDatabaseValidation = args.Contains("--validate-database", StringComparer.OrdinalIgnoreCase);
if (runDatabaseInitialization || runDatabaseValidation)
{
    using var scope = app.Services.CreateScope();
    var dbCtx = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
    var platformScope = scope.ServiceProvider.GetRequiredService<IPlatformScopeFactory>();

    // CLI baseline 初始化/校验同样无 HTTP/无租户上下文：seeder 经 EF 写 ITenantScoped 实体（如 BasicDataSeeder 播 FinAuxiliaryItem）
    // 会撞 fail-closed 写硬墙——与启动迁移块同法包受控平台作用域放行。
    using (platformScope.Enter(runDatabaseInitialization ? "cli-init-database" : "cli-validate-database"))
    {
        var report = runDatabaseInitialization
            ? seeder.InitializeNewDatabase(dbCtx)
            : seeder.ValidateDatabase(dbCtx);

        foreach (var step in report.Steps)
        {
            app.Logger.LogInformation("Database baseline step: {Step}", step);
        }

        if (!report.Success)
        {
            foreach (var issue in report.Issues)
            {
                app.Logger.LogError("Database baseline issue: {Issue}", issue);
            }

            Environment.ExitCode = 2;
            return;
        }

        app.Logger.LogInformation(
            runDatabaseInitialization
                ? "数据库 baseline 初始化完成"
                : "数据库 baseline 校验通过");
    }
    return;
}

// InMemory 自动初始化已移除，数据库初始化改为通过前端"数据库初始化"页面手动触发

// 数据库版本化迁移
using (var scope = app.Services.CreateScope())
{
    var dbCtx = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
    var platformScope = scope.ServiceProvider.GetRequiredService<IPlatformScopeFactory>();
    try
    {
        // 启动期无 HTTP/无租户上下文：包受控平台作用域，令 seeder 中经 EF 写入 ITenantScoped 实体
        // （如 BasicDataSeeder 播 FinAuxiliaryItem）不撞 fail-closed 写硬墙、读 ITenantScoped 表不落空集。
        using (platformScope.Enter("startup-migration"))
        {
            seeder.MigrateAll(dbCtx);
        }
        app.Logger.LogInformation("数据库迁移执行完成");
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "数据库迁移执行失败，应用无法启动");
        throw; // fail-fast: 迁移失败不允许启动
    }
}

// 数据迁移：将未分配账套的凭证分配到默认账套
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
        var platformScope = scope.ServiceProvider.GetRequiredService<IPlatformScopeFactory>();
        // FinAccountSet/FinVoucher 均为 ITenantScoped：启动期无租户上下文下裸读会被 fail-closed 过滤器落空集，
        // 使本回填静默失效。包平台作用域放行读，恢复迁移语义。
        using (platformScope.Enter("voucher-accountset-backfill"))
        {
            var defaultAccountSet = dbContext.Set<STOTOP.Module.Finance.Entities.FinAccountSet>()
                .Where(a => a.FIsDefault)
                .OrderBy(a => a.FID)
                .FirstOrDefault();
            if (defaultAccountSet != null)
            {
                var count = dbContext.Set<STOTOP.Module.Finance.Entities.FinVoucher>()
                    .Count(v => v.FAccountSetId == 0);
                if (count > 0)
                {
                    dbContext.Database.ExecuteSqlRaw(
                        "UPDATE [FIN凭证] SET [F账套ID] = {0} WHERE [F账套ID] = 0",
                        defaultAccountSet.FID);
                    app.Logger.LogInformation("已将 {Count} 张凭证分配到默认账套 {Name}", count, defaultAccountSet.FName);
                }
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "凭证账套迁移失败（可能数据库未初始化）");
    }
}

// ========== 安全自检：默认管理员口令 & 加密密钥 ==========
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
        var adminId = dbContext.Set<STOTOP.Module.System.Entities.SysUser>()
            .IgnoreQueryFilters()
            .Where(u => u.FAccount == "admin")
            .Select(u => (long?)u.FID)
            .FirstOrDefault();
        if (adminId.HasValue)
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            if (await authService.VerifyPassword(adminId.Value, "admin123"))
            {
                app.Logger.LogCritical(
                    "【安全告警】管理员账号 admin 仍在使用默认口令 admin123，请立即登录修改！这是高危默认凭据。");
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "默认管理员口令安全自检失败（可能数据库未初始化）");
    }

    if (DbConnectionsHelper.IsUsingDefaultEncryptionKey(app.Configuration["Security:EncryptionKey"])
        && !app.Environment.IsDevelopment())
    {
        app.Logger.LogWarning(
            "【安全告警】未配置 Security:EncryptionKey，正在使用源码内置默认加密密钥；db-connections.json 的数据库口令与 dingtalk-config.json 的 AppSecret 可被持有程序文件者解密。请设置环境变量 Security__EncryptionKey 后重新保存相关配置。");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "STOTOP API v1");
    });
}

// app.UseHttpsRedirection(); // 开发环境使用HTTP，不需要HTTPS重定向

// Database Setup Middleware - 在其他中间件之前注册
app.UseDatabaseSetupMiddleware();

// Global Exception Middleware
app.UseGlobalExceptionMiddleware();

app.UseCors("AllowFrontend");

app.UseResponseCompression();

// 静态文件服务（自定义 MIME 类型处理）
var defaultProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
defaultProvider.Mappings[".wasm"] = "application/wasm";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = defaultProvider,
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});

// 附件静态访问：/uploads/attachments 映射到 ContentRoot/uploads/attachments
var attachmentsPath = Path.Combine(app.Environment.ContentRootPath, "uploads", "attachments");
Directory.CreateDirectory(attachmentsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(app.Environment.ContentRootPath, "uploads")),
    RequestPath = "/uploads"
});

app.UseAuthentication();
app.UseMiddleware<TokenSlidingRefreshMiddleware>();   // 新增
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

// 组织上下文中间件 - 在认证授权之后注入当前组织信息
app.UseMiddleware<OrgContextMiddleware>();

// 欠费冻结白名单（阶段4B·D7）- 须在 OrgContextMiddleware 之后（CurrentTenantId 已就绪）。
// 单客户(正式态)下恒放行=休眠能力；租户欠费冻结时按白名单拒业务写与批量导出。
app.UseMiddleware<TenantFreezeMiddleware>();

// Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    DashboardTitle = "STOTOP 任务调度",
    // 仅本地回环或已认证管理员可访问，避免匿名远程触发/删除定时任务
    Authorization = new[] { new HangfireDashboardAuthorizationFilter() },
    IsReadOnlyFunc = (Hangfire.Dashboard.DashboardContext context) => false
});

app.MapControllers();

// Health Check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Name == "database"
});

app.MapHub<ProgressHub>("/hubs/progress");
app.MapHub<DatabaseProgressHub>("/hubs/database-progress");
app.MapHub<NotificationHub>("/hubs/notification");
app.MapHub<WorkHubHub>("/hubs/workhub");
app.MapHub<CardFlowHub>("/hubs/cardflow");

// 启动时根据备份配置注册 Hangfire 定时任务
try
{
    var backupConfig = DbConnectionsHelper.LoadBackupConfig();
    if (backupConfig.Enabled && !string.IsNullOrWhiteSpace(backupConfig.CronExpression))
    {
        RecurringJob.AddOrUpdate<DatabaseService>(
            "database-auto-backup",
            service => service.ExecuteScheduledBackupAsync(),
            backupConfig.CronExpression);
        app.Logger.LogInformation("已注册数据库定时备份任务: {Cron}", backupConfig.CronExpression);
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "注册定时备份任务失败");
}

// 合同到期提醒 - 每日凌晨1点执行
RecurringJob.AddOrUpdate<ContractExpiryReminderJob>(
    "contract-expiry-reminder",
    job => job.ExecuteAsync(),
    "0 1 * * *"); // 每天01:00
app.Logger.LogInformation("已注册合同到期提醒定时任务");

// 清理过期会话 - 每小时执行一次
RecurringJob.AddOrUpdate<SessionService>(
    "clean-expired-sessions",
    service => service.CleanExpiredSessions(),
    Cron.Hourly);
app.Logger.LogInformation("已注册过期会话清理定时任务");

// WF 工作项超时检查（每5分钟）
RecurringJob.AddOrUpdate<WorkItemTimeoutJob>(
    "workflow-item-timeout",
    job => job.ExecuteAsync(),
    "*/5 * * * *");
app.Logger.LogInformation("已注册工作项超时处理定时任务");

// 孤立数据定期监控（每天凌晨3点）：检测因批次物理删除后残留的孤立工作项/凭证/暂存行
RecurringJob.AddOrUpdate<STOTOP.Module.CardFlow.Jobs.OrphanedDataMonitorJob>(
    "orphaned-data-monitor",
    job => job.ExecuteAsync(),
    "0 3 * * *");
app.Logger.LogInformation("已注册孤立数据监控定时任务");

// 质量问题超时标记（每小时执行）
RecurringJob.AddOrUpdate<QualityOverdueCheckJob>(
    "quality-overdue-check",
    job => job.ExecuteAsync(),
    Cron.Hourly);
app.Logger.LogInformation("已注册质量问题超时标记定时任务");

// 申通统一质控归一（每日 06:00 执行）：枚举有申通数据的组织逐个归一全 29 源（单组织异常隔离）。
RecurringJob.AddOrUpdate<ShentongUnificationJob>(
    "shentong-unify",
    job => job.ExecuteAsync(),
    "0 6 * * *"); // 每天 06:00
app.Logger.LogInformation("已注册申通统一质控归一定时任务（Cron: 0 6 * * *）");

// CardFlow 节点超时检查 - 每小时
RecurringJob.AddOrUpdate<STOTOP.Module.CardFlow.Jobs.CardFlowTimeoutJob>(
    "cardflow-stage-timeout",
    job => job.ExecuteAsync(),
    "0 * * * *");
app.Logger.LogInformation("已注册CardFlow节点超时检查定时任务");

// CardFlow 推送失败重试 - 每5分钟
RecurringJob.AddOrUpdate<STOTOP.Module.CardFlow.Jobs.PushRetryJob>(
    "cardflow-push-retry",
    job => job.ExecuteAsync(),
    "*/5 * * * *");
app.Logger.LogInformation("已注册CardFlow推送失败重试定时任务");

// 积分 B 分月清 - 每月 1 日 02:00 执行
RecurringJob.AddOrUpdate<STOTOP.Module.Points.Jobs.PointMonthResetJob>(
    "points-month-reset",
    job => job.ExecuteAsync(),
    "0 2 1 * *");
app.Logger.LogInformation("已注册积分月清定时任务（Cron: 0 2 1 * *）");

// 积分 B 分年清 - 每年 1 月 1 日 02:30 执行
RecurringJob.AddOrUpdate<STOTOP.Module.Points.Jobs.PointYearResetJob>(
    "points-year-reset",
    job => job.ExecuteAsync(),
    "30 2 1 1 *");

// KSF 月度核算 - 每月 6 日 03:00 执行（上月期间）
RecurringJob.AddOrUpdate<STOTOP.Module.KSF.Jobs.KsfCalcJob>(
    "ksf.calc-monthly",
    job => job.ExecuteAsync(null, null),
    "0 3 6 * *");
app.Logger.LogInformation("已注册 KSF 月度核算定时任务（Cron: 0 3 6 * *）");

// PPV 月度汇总 - 每月 6 日 04:00 执行（上月期间，在 KSF 之后）
RecurringJob.AddOrUpdate<STOTOP.Module.PPV.Jobs.PpvCalcJob>(
    "ppv.aggregate-monthly",
    job => job.Execute(null, null),
    "0 4 6 * *");
app.Logger.LogInformation("已注册 PPV 月度汇总定时任务（Cron: 0 4 6 * *）");

// B 分月清兑换 - 每月 6 日 04:30 执行（在 KSF/PPV 之后、工资结算之前）
RecurringJob.AddOrUpdate<STOTOP.Module.Salary.Jobs.BScoreResetJob>(
    "sal.bscore-monthly-reset",
    job => job.Execute(null, null),
    "0 30 4 6 * *");
app.Logger.LogInformation("已注册 B 分月清兑换定时任务（Cron: 0 30 4 6 * *）");

// SAL 月度工资结算 - 每月 6 日 05:00 执行（上月期间，在 B分月清之后）
RecurringJob.AddOrUpdate<STOTOP.Module.Salary.Jobs.SalarySettlementJob>(
    "sal.monthly-settlement",
    job => job.Execute(null, null),
    "0 5 6 * *");
app.Logger.LogInformation("已注册 SAL 月度工资结算定时任务（Cron: 0 5 6 * *）");

// SAL 晋升扫描 - 每月 1 日 08:00 执行（扫描 A 分达标员工触发晋升评审）
RecurringJob.AddOrUpdate<STOTOP.Module.Salary.Jobs.PromotionScanJob>(
    "sal.promotion-scan",
    job => job.Execute(null),
    "0 8 1 * *");
app.Logger.LogInformation("已注册 SAL 晋升扫描定时任务（Cron: 0 8 1 * *）");

app.Logger.LogInformation("已注册积分年清定时任务（Cron: 30 2 1 1 *）");

// 钉钉定时自动同步
try
{
    var dingTalkConfig = DingTalkConfigHelper.GetGlobalConfig();
    if (dingTalkConfig?.AutoSync == 1 && !string.IsNullOrWhiteSpace(dingTalkConfig.SyncCron))
    {
        RecurringJob.AddOrUpdate<IDingTalkService>(
            "dingtalk-auto-sync",
            service => service.FullSyncAllTenantsAsync(),
            dingTalkConfig.SyncCron);
        app.Logger.LogInformation("已注册钉钉定时自动同步任务，Cron: {Cron}", dingTalkConfig.SyncCron);
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "注册钉钉定时同步任务失败");
}

// 钉钉群机器人推送任务（告警/日报/周报）— 依赖群机器人 Webhook 配置
if (!string.IsNullOrWhiteSpace(app.Configuration["DingTalk:RobotWebhookUrl"]))
{
    // 异常告警检查 - 每小时整点执行
    RecurringJob.AddOrUpdate<AlertBotJob>(
        "dingtalk-alert-check",
        job => job.Execute(),
        "0 */1 * * *");

    // 经营日报推送 - 周一至周六 08:00
    RecurringJob.AddOrUpdate<DailyReportBotJob>(
        "dingtalk-daily-report",
        job => job.Execute(),
        "0 8 * * 1-6");

    // 经营周报推送 - 每周一 09:00
    RecurringJob.AddOrUpdate<WeeklyReportBotJob>(
        "dingtalk-weekly-report",
        job => job.Execute(),
        "0 9 * * 1");

    app.Logger.LogInformation("已注册钉钉机器人推送定时任务（告警/日报/周报）");
}
else
{
    app.Logger.LogInformation("未配置钉钉机器人 Webhook，跳过注册告警/日报/周报推送任务");
}

app.Run();

// CfProgressNotifierBridge 已删除：IProgressNotifier 统一为 STOTOP.Module.CardFlow.AutoPlugin.IProgressNotifier，无需桥接
