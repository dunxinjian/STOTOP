using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using STOTOP.Module.Express.Configurations;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using Xunit;

namespace STOTOP.Module.Express.Tests.Schema;

public class NetworkPointSchemaContractTests
{
    [Fact]
    public void ExpNetworkPoint_model_uses_parent_point_code_and_has_no_source_uid()
    {
        var modelBuilder = new ModelBuilder();
        new ExpNetworkPointConfiguration().Configure(modelBuilder.Entity<ExpNetworkPoint>());
        var entityType = modelBuilder.Model.FindEntityType(typeof(ExpNetworkPoint))!;
        var table = StoreObjectIdentifier.Table("EXP快递网点", null);
        var columns = entityType.GetProperties()
            .Select(p => p.GetColumnName(table))
            .Where(name => name != null)
            .ToList();

        Assert.DoesNotContain("F源UID", columns);
        Assert.DoesNotContain("F上级网点源UID", columns);
        Assert.Contains("F上级网点编号", columns);
        Assert.Null(typeof(ExpNetworkPoint).GetProperty("FSourceUid"));
        Assert.NotNull(typeof(ExpNetworkPoint).GetProperty("FParentPointCode"));
        Assert.NotNull(typeof(NetworkPointDto).GetProperty("ParentPointCode"));
        Assert.NotNull(typeof(CreateNetworkPointRequest).GetProperty("ParentPointCode"));
        Assert.NotNull(typeof(UpdateNetworkPointRequest).GetProperty("ParentPointCode"));
    }

    [Fact]
    public void ExpressSeeder_has_a_migration_for_network_point_parent_code_schema()
    {
        var seederPath = FindRepositoryFile("src/STOTOP.WebAPI/Data/Seeders/ExpressSeeder.cs");
        var source = File.ReadAllText(seederPath);

        Assert.Contains("MigrateV16", source);
        Assert.Contains("F上级网点源UID", source);
        Assert.Contains("F上级网点编号", source);
        Assert.Contains("EXEC sp_executesql N'UPDATE [EXP快递网点]", source);
        Assert.Contains("DropColumnSafe(context, \"EXP快递网点\", \"F源UID\")", source);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file {relativePath}");
    }
}
