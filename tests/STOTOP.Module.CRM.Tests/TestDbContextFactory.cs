using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Tests;

public static class TestDbContextFactory
{
    /// <summary>每次创建独立 InMemory 库（GUID 后缀），测试间天然隔离，绝不触碰共享远程库。</summary>
    public static STOTOPDbContext Create(string databaseName, long? orgId = null)
    {
        STOTOPDbContext.RegisterModuleAssembly(typeof(CrmCustomer).Assembly);

        var options = new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseInMemoryDatabase($"{databaseName}_{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;

        return new STOTOPDbContext(options, new TestOrgContextAccessor { CurrentOrgId = orgId });
    }

    /// <summary>共享同一 InMemory 库（库名固定，不加 GUID），用于组织隔离测试：
    /// 多个不同 orgId 的上下文读写同一份数据，验证全局查询过滤器生效。</summary>
    public static STOTOPDbContext CreateShared(string databaseName, long? orgId)
    {
        STOTOPDbContext.RegisterModuleAssembly(typeof(CrmCustomer).Assembly);

        var options = new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new STOTOPDbContext(options, new TestOrgContextAccessor { CurrentOrgId = orgId });
    }

    private sealed class TestOrgContextAccessor : IOrgContextAccessor
    {
        public long? CurrentOrgId { get; set; }
        public long? CurrentTenantId { get; set; } = 1;
        public bool IsPlatformScope { get; set; }
    }
}
