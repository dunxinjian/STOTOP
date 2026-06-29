using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace STOTOP.Module.Finance.Tests.Voucher;

// 测试专用：在默认模型构建后，剥离 SQL Server 专属列类型/默认值/计算列，使 SQLite 能 EnsureCreated 整模型。
public sealed class SqliteCompatModelCustomizer : ModelCustomizer
{
    public SqliteCompatModelCustomizer(ModelCustomizerDependencies dependencies) : base(dependencies) { }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context); // 先跑 STOTOPDbContext.OnModelCreating
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var p in entity.GetProperties())
            {
                var ct = p.GetColumnType();
                if (!string.IsNullOrEmpty(ct) &&
                    (ct.Contains("max") || ct.Contains("datetime2") || ct.Contains("datetimeoffset")
                     || ct.Contains("nvarchar") || ct.Contains("varchar") || ct.Contains("rowversion")
                     || ct.Contains("money") || ct.Contains("uniqueidentifier")))
                    p.SetColumnType(null); // 让 SQLite 用默认类型
                if (p.GetDefaultValueSql() != null) p.SetDefaultValueSql(null);
                if (p.GetComputedColumnSql() != null) p.SetComputedColumnSql(null);
            }
        }
    }
}
