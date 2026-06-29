using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STOTOP.Core.Models;
using STOTOP.Core.Services;
using System.Reflection;

namespace STOTOP.Infrastructure.Data;

public class STOTOPDbContext : DbContext
{
    // 使用不可变集合，确保所有实例共享配置程序集（线程安全）
    private static volatile ImmutableList<Assembly> _configurationAssemblies = ImmutableList<Assembly>.Empty;
    private static readonly object _lock = new();

    private readonly IOrgContextAccessor? _orgContextAccessor;
    private bool _suppressOrgIdFill;

    /// <summary>
    /// 供全局查询过滤器引用，EF Core 每次查询时会重新求值
    /// </summary>
    private long? CurrentOrgId => _orgContextAccessor?.CurrentOrgId;

    public STOTOPDbContext(DbContextOptions<STOTOPDbContext> options, IOrgContextAccessor? orgContextAccessor = null)
        : base(options)
    {
        _orgContextAccessor = orgContextAccessor;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.ConfigureWarnings(w =>
        {
            // 抑制导航属性与查询过滤器交互警告（10622）
            // 父实体有全局查询过滤器，子实体通过导航属性级联隔离，这是有意的设计
            w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning);

            // 抑制 IEntityTypeConfiguration 扫描警告（10632）
            // Infrastructure 程序集中没有配置类，配置类分布在各模块程序集中
            w.Ignore(CoreEventId.NoEntityTypeConfigurationsWarning);
        });
    }

    /// <summary>
    /// 静态注册模块配置程序集（推荐在 app.Build() 之前调用，确保模型构建时所有配置已就绪）
    /// </summary>
    public static void RegisterModuleAssembly(Assembly assembly)
    {
        lock (_lock)
        {
            if (!_configurationAssemblies.Contains(assembly))
            {
                _configurationAssemblies = _configurationAssemblies.Add(assembly);
            }
        }
    }

    /// <summary>
    /// 实例方法注册（保留向后兼容）
    /// </summary>
    public void AddConfigurationAssembly(Assembly assembly)
    {
        RegisterModuleAssembly(assembly);
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // 应用当前程序集的配置
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(STOTOPDbContext).Assembly);
        
        // 应用外部模块程序集的配置
        foreach (var assembly in _configurationAssemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }

        // 为所有继承自 BaseEntity 的实体配置主键（在配置应用后）
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var entityBuilder = modelBuilder.Entity(entityType.ClrType);
                entityBuilder.HasKey("FID");
                // 显式标记 FID 由数据库生成（解决 BaseEntity 无 [Key] 特性导致 conventions 未生效的问题）
                entityBuilder.Property("FID").ValueGeneratedOnAdd();
            }
        }

        // EF Core 全局修复：HasDefaultValue() 会隐式设置 ValueGenerated.OnAdd，
        // 导致 SQL Server 将非主键 int/long 属性也生成为 IDENTITY 列。
        // 此处显式将非主键 int/long 属性的 ValueGenerated 重置为 Never，
        // 仅保留 DEFAULT 约束，不生成 IDENTITY。
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var pk = entityType.FindPrimaryKey();
            foreach (var property in entityType.GetProperties())
            {
                if ((property.ClrType == typeof(int) || property.ClrType == typeof(long))
                    && property.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd
                    && (pk == null || !pk.Properties.Contains(property)))
                {
                    property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
                }
            }
        }

        // 不再全局修改所有外键的 DeleteBehavior
        // EF Core 默认行为：必需关系=Cascade，可选关系=ClientSetNull
        // 仅对已知会导致 SQL Server 多重级联路径的关系，在各自的 EntityConfiguration 中单独设置 Restrict

        // === 组织数据隔离全局过滤器 ===
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IOrgScoped).IsAssignableFrom(entityType.ClrType))
            {
                _configureOrgFilterMethod.MakeGenericMethod(entityType.ClrType)
                    .Invoke(null, new object[] { modelBuilder, this });
            }
        }

        // === IOrgOwned 过滤器（组织扩展实体的数据隔离） ===
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IOrgOwned).IsAssignableFrom(entityType.ClrType))
            {
                _configureOrgOwnerFilterMethod.MakeGenericMethod(entityType.ClrType)
                    .Invoke(null, new object[] { modelBuilder, this });
            }
        }
    }

    private static readonly MethodInfo _configureOrgFilterMethod =
        typeof(STOTOPDbContext).GetMethod(nameof(ConfigureOrgFilter), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void ConfigureOrgFilter<TEntity>(ModelBuilder modelBuilder, STOTOPDbContext context)
        where TEntity : class, IOrgScoped
    {
        // 去模板化：仅按当前组织过滤，不再放行 FOrgId == 0 的全局共享数据。
        // 跨组织读取请使用 IgnoreQueryFilters() 或临时切换 IOrgContextAccessor.CurrentOrgId。
        modelBuilder.Entity<TEntity>().HasQueryFilter(
            e => context.CurrentOrgId == null || e.FOrgId == context.CurrentOrgId
        );
    }

    private static readonly MethodInfo _configureOrgOwnerFilterMethod =
        typeof(STOTOPDbContext).GetMethod(nameof(ConfigureOrgOwnerFilter), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void ConfigureOrgOwnerFilter<TEntity>(ModelBuilder modelBuilder, STOTOPDbContext context)
        where TEntity : class, IOrgOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(
            e => context.CurrentOrgId == null || e.FOwnerOrgId == context.CurrentOrgId || e.FOwnerOrgId == 0
        );
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        FillOrgIdForNewEntities();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        FillOrgIdForNewEntities();
        return base.SaveChanges();
    }

    /// <summary>在返回的作用域内，SaveChanges 不对 FOrgId==0 的新 IOrgScoped 实体自动填充当前组织。
    /// 仅用于「有意创建全局(FOrgId=0)记录」这类受控内部场景（如保存为全局模板）。</summary>
    public IDisposable SuppressOrgIdFill()
    {
        _suppressOrgIdFill = true;
        return new SuppressOrgIdFillScope(() => _suppressOrgIdFill = false);
    }

    private sealed class SuppressOrgIdFillScope : IDisposable
    {
        private readonly Action _onDispose;
        public SuppressOrgIdFillScope(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }

    private void FillOrgIdForNewEntities()
    {
        if (_suppressOrgIdFill)
        {
            return;
        }

        var currentOrgId = _orgContextAccessor?.CurrentOrgId;
        if (currentOrgId.HasValue)
        {
            foreach (var entry in ChangeTracker.Entries<IOrgScoped>())
            {
                if (entry.State == EntityState.Added && entry.Entity.FOrgId == 0)
                {
                    entry.Entity.FOrgId = currentOrgId.Value;
                }
            }

            // IOrgOwned 实体自动填充 FOwnerOrgId
            foreach (var entry in ChangeTracker.Entries<IOrgOwned>())
            {
                if (entry.State == EntityState.Added && entry.Entity.FOwnerOrgId == 0)
                {
                    entry.Entity.FOwnerOrgId = currentOrgId.Value;
                }
            }
        }
    }
}
