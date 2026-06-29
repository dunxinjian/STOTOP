using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.AutoPlugin;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 质量问题类型注册器 - 应用启动时从所有 IQualityIssueTypeProvider 收集定义并同步到数据库
/// </summary>
public class QualityIssueTypeRegistrar : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QualityIssueTypeRegistrar> _logger;

    public QualityIssueTypeRegistrar(
        IServiceProvider serviceProvider,
        ILogger<QualityIssueTypeRegistrar> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var providers = scope.ServiceProvider.GetServices<IQualityIssueTypeProvider>();
            var dbContext = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();

            // 检查表是否存在，首次启动时 CreateMissingTables 可能尚未成功创建该表
            if (!await TableExistsAsync(dbContext, "CF质量问题类型", cancellationToken))
            {
                _logger.LogWarning("表 CF质量问题类型 尚未创建，跳过质量问题类型同步（将在下次重启时自动创建）");
                return;
            }

            var allDefinitions = providers.SelectMany(p => p.GetIssueTypeDefinitions()).ToList();
            if (allDefinitions.Count == 0)
            {
                _logger.LogInformation("未发现任何 IQualityIssueTypeProvider 注册的质量问题类型");
                return;
            }

            _logger.LogInformation("开始同步质量问题类型定义，共 {Count} 条", allDefinitions.Count);

            var existingTypes = await dbContext.Set<CfQualityIssueType>()
                .ToListAsync(cancellationToken);

            var existingByCode = existingTypes.ToDictionary(t => t.FCode, StringComparer.OrdinalIgnoreCase);

            int inserted = 0, updated = 0, skipped = 0;

            foreach (var def in allDefinitions)
            {
                if (existingByCode.TryGetValue(def.Code, out var existing))
                {
                    if (existing.FIsBuiltIn)
                    {
                        // 内置类型：仅更新元数据字段，不碰派发配置
                        existing.FName = def.Name;
                        existing.FDescription = def.Description;
                        existing.FModule = def.Module;
                        existing.FSourceAutoPlugin = def.SourceAutoPlugin;
                        existing.FSeverityLevel = def.SeverityLevel;
                        existing.FCategory = def.Category;
                        existing.FDetailRoute = def.DetailRoute;
                        existing.FSuggestedFix = def.SuggestedFix;
                        existing.FUpdatedTime = DateTime.Now;
                        updated++;
                    }
                    else
                    {
                        // 非内置类型（用户自定义）：完全不碰
                        skipped++;
                    }
                }
                else
                {
                    // 新记录：插入，FDispatchMode=NULL（待配置）
                    dbContext.Set<CfQualityIssueType>().Add(new CfQualityIssueType
                    {
                        FCode = def.Code,
                        FName = def.Name,
                        FDescription = def.Description,
                        FModule = def.Module,
                        FSourceAutoPlugin = def.SourceAutoPlugin,
                        FSeverityLevel = def.SeverityLevel,
                        FCategory = def.Category,
                        FIsBuiltIn = true,
                        FSuggestedFix = def.SuggestedFix,
                        FDetailRoute = def.DetailRoute,
                        FDispatchMode = null, // 待配置
                        FDispatchTarget = null,
                        FResolveMode = null,
                        FAggregationMode = "BatchIssue",
                        FOrgScoped = true,
                        FTimeoutHours = 0,
                        FStatus = 1,
                        FCreatedTime = DateTime.Now,
                        FUpdatedTime = DateTime.Now,
                    });
                    inserted++;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("质量问题类型同步完成：新增 {Inserted}，更新 {Updated}，跳过 {Skipped}",
                inserted, updated, skipped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "质量问题类型同步失败");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 检查数据库中指定表是否存在
    /// </summary>
    private static async Task<bool> TableExistsAsync(STOTOPDbContext dbContext, string tableName, CancellationToken ct)
    {
        var connection = dbContext.Database.GetDbConnection();
        var wasOpen = connection.State == global::System.Data.ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(ct);
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}' AND TABLE_SCHEMA = 'dbo') THEN 1 ELSE 0 END";
            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result) == 1;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }
}
