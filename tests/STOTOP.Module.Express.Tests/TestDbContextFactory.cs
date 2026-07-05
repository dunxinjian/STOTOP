using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Express.Tests;

public static class TestDbContextFactory
{
    public static STOTOPDbContext Create(string databaseName, long? orgId = null)
    {
        STOTOPDbContext.RegisterModuleAssembly(typeof(ExpBillingResult).Assembly);
        // 模型含核心 System 实体（SysUser 等）的导航配置，DB 集成测试需注册其配置程序集
        STOTOPDbContext.RegisterModuleAssembly(typeof(SysUser).Assembly);

        var options = new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseInMemoryDatabase($"{databaseName}_{Guid.NewGuid():N}")
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
