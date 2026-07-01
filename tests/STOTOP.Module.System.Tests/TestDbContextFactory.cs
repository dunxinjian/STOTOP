using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 隔离自检用 DbContext 工厂：全模块已注册（<see cref="TenantTestModules"/> 于程序集加载时注册），
/// 每次用 Guid 后缀的 InMemory 库隔离，注入可控的 组织/租户/平台 上下文。
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>测试默认租户id。</summary>
    public const long DefaultTenantId = 1;

    public static STOTOPDbContext Create(string databaseName, long? orgId = null, long? tenantId = DefaultTenantId, bool platformScope = false)
    {
        TenantTestModules.RegisterAll();

        var options = new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseInMemoryDatabase($"{databaseName}_{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;

        return new STOTOPDbContext(options, new TestContextAccessor
        {
            CurrentOrgId = orgId,
            CurrentTenantId = tenantId,
            IsPlatformScope = platformScope,
        });
    }

    /// <summary>可控上下文访问器（组织/租户/平台三维），供隔离自检在同一库上构造不同上下文。</summary>
    public sealed class TestContextAccessor : IOrgContextAccessor
    {
        public long? CurrentOrgId { get; set; }
        public long? CurrentTenantId { get; set; }
        public bool IsPlatformScope { get; set; }
    }
}
