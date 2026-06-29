using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinMigrationSchemeConfiguration : IEntityTypeConfiguration<FinMigrationScheme>
{
    public void Configure(EntityTypeBuilder<FinMigrationScheme> builder)
    {
        builder.ToTable("F账套迁移_方案");
        builder.HasKey(e => e.FID);

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.F方案名称).HasColumnName("F方案名称").HasMaxLength(200);
        builder.Property(e => e.F源账套标识).HasColumnName("F源账套标识").HasMaxLength(1000);
        builder.Property(e => e.F目标账套ID).HasColumnName("F目标账套ID");
        builder.Property(e => e.F辅助项缺失策略).HasColumnName("F辅助项缺失策略").HasMaxLength(20);
        builder.Property(e => e.F说明).HasColumnName("F说明").HasMaxLength(500);
        builder.Property(e => e.F状态).HasColumnName("F状态");
        builder.Property(e => e.F组织ID).HasColumnName("F组织ID");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间");
        builder.Property(e => e.F更新时间).HasColumnName("F更新时间");

        builder.HasIndex(e => e.F组织ID).HasDatabaseName("IX_F账套迁移_方案_组织ID");
        builder.HasIndex(e => e.F目标账套ID).HasDatabaseName("IX_F账套迁移_方案_目标账套ID");

        builder.HasMany(e => e.AccountMappings)
            .WithOne(e => e.Scheme)
            .HasForeignKey(e => e.F方案ID)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.AuxMappings)
            .WithOne(e => e.Scheme)
            .HasForeignKey(e => e.F方案ID)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.AssetMappings)
            .WithOne(e => e.Scheme)
            .HasForeignKey(e => e.F方案ID)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
