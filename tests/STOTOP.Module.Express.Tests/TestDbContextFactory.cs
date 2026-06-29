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

        return new STOTOPDbContext(options, orgId.HasValue ? new TestOrgContextAccessor(orgId.Value) : null);
    }

    private sealed class TestOrgContextAccessor(long orgId) : IOrgContextAccessor
    {
        public long? CurrentOrgId { get; set; } = orgId;
    }
}
