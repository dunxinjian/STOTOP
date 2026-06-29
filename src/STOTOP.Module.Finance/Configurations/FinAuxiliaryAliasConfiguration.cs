using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAuxiliaryAliasConfiguration : IEntityTypeConfiguration<FinAuxiliaryAlias>
{
    public void Configure(EntityTypeBuilder<FinAuxiliaryAlias> builder)
    {
        builder.ToTable("FIN辅助核算别名");

        builder.HasKey(e => e.FID);
        builder.Property(e => e.FID).HasColumnName("FID").HasDefaultValueSql("newid()");
        builder.Property(e => e.F辅助核算项目ID).HasColumnName("F辅助核算项目ID");
        builder.Property(e => e.F别名).HasColumnName("F别名").HasMaxLength(200);
        builder.Property(e => e.F辅助类型).HasColumnName("F辅助类型").HasMaxLength(50);
        builder.Property(e => e.F组织ID).HasColumnName("F组织ID");

        builder.HasIndex(e => new { e.F辅助类型, e.F别名 }).HasDatabaseName("IX_FIN辅助核算别名_查询");
    }
}
