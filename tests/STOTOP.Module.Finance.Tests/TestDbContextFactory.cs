using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Tests;

public static class TestDbContextFactory
{
    /// <summary>测试默认租户id（v2 多租户隔离；现有测试透明运行在该租户内，读写自洽）。</summary>
    public const long DefaultTenantId = 1;

    public static STOTOPDbContext Create(string databaseName, long? orgId = null, long? tenantId = DefaultTenantId, bool platformScope = false)
    {
        STOTOPDbContext.RegisterModuleAssembly(typeof(FinAmoebaPLTemplate).Assembly);

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

    /// <summary>测试用上下文访问器（含 v2 租户字段；公开，供隔离自检在同一库上构造不同租户的上下文）。</summary>
    public sealed class TestContextAccessor : IOrgContextAccessor
    {
        public long? CurrentOrgId { get; set; }
        public long? CurrentTenantId { get; set; }
        public bool IsPlatformScope { get; set; }
    }
}
