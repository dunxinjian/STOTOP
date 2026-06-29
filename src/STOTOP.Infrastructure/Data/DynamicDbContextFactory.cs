using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace STOTOP.Infrastructure.Data;

/// <summary>
/// 动态 DbContext 工厂，用于在运行时创建具有不同数据库配置的 DbContext
/// 避免 EF Core 模型缓存导致的切换数据库提供者失效问题
/// </summary>
public interface IDynamicDbContextFactory
{
    STOTOPDbContext CreateDbContext(string provider, string? connectionString = null);
}

public class DynamicDbContextFactory : IDynamicDbContextFactory
{
    private readonly ILoggerFactory? _loggerFactory;

    public DynamicDbContextFactory(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
    }

    public STOTOPDbContext CreateDbContext(string provider, string? connectionString = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<STOTOPDbContext>();

        if (_loggerFactory != null)
            optionsBuilder.UseLoggerFactory(_loggerFactory);

        // 仅支持 SqlServer
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentException("SqlServer 连接字符串不能为空");
        optionsBuilder.UseSqlServer(connectionString, opts => opts.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null));

        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);

        var dbContext = new STOTOPDbContext(optionsBuilder.Options);

        // 确保所有模块配置程序集已注册（正常运行时 Program.cs 已静态注册，此处为运行时动态创建 DbContext 的安全冗余）
        var moduleAssemblies = new[]
        {
            "STOTOP.Module.System",
            "STOTOP.Module.Finance",
            "STOTOP.Module.Supplier",
            "STOTOP.Module.HR",
            "STOTOP.Module.Dormitory",
            "STOTOP.Module.Vehicle",
            "STOTOP.Module.Express",
            "STOTOP.Module.Points",
            "STOTOP.Module.Task",
            "STOTOP.Module.CRM",
            "STOTOP.Module.Contract",
            "STOTOP.Module.Quality",
            "STOTOP.Module.Conference",
            "STOTOP.Module.Insurance",
            "STOTOP.Module.Workflow",
        };
        foreach (var name in moduleAssemblies)
        {
            try
            {
                dbContext.AddConfigurationAssembly(Assembly.Load(name));
            }
            catch (Exception)
            {
                // 程序集可能尚未加载，静默跳过
            }
        }

        return dbContext;
    }
}
