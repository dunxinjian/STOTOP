using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Tests;

public static class TestDbContextFactory
{
    public static STOTOPDbContext Create(string databaseName, long? orgId = null)
    {
        STOTOPDbContext.RegisterModuleAssembly(typeof(ExpBillingResult).Assembly);

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
