using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.KSF.Entities;

namespace STOTOP.Module.KSF.Configurations;

public class KsfEmployeeUnitMappingConfiguration : IEntityTypeConfiguration<KsfEmployeeUnitMapping>
{
    public void Configure(EntityTypeBuilder<KsfEmployeeUnitMapping> builder)
    {
        builder.ToTable("KSF员工经营单元映射");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F员工ID).HasColumnName("F员工ID");
        builder.Property(e => e.F经营单元ID).HasColumnName("F经营单元ID");
        builder.Property(e => e.F分摊比例).HasColumnName("F分摊比例").HasColumnType("decimal(18,4)").HasDefaultValue(1.0m);
        builder.Property(e => e.F生效起期).HasColumnName("F生效起期");
        builder.Property(e => e.F生效止期).HasColumnName("F生效止期");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F员工ID, e.F经营单元ID, e.F生效起期 })
            .IsUnique()
            .HasDatabaseName("UQ_KSF员工经营单元_员工_单元_生效期");
    }
}
