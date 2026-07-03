using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinOperatingUnitConfiguration : IEntityTypeConfiguration<FinOperatingUnit>
{
    public void Configure(EntityTypeBuilder<FinOperatingUnit> builder)
    {
        builder.ToTable("FIN经营单元");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.Property(e => e.FCompanyId).HasColumnName("F网点公司ID");
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50);
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FSourceType).HasColumnName("F来源类型").HasMaxLength(20);
        builder.Property(e => e.FSourceLegacyAuxId).HasColumnName("F来源业务单元ID");
        builder.Property(e => e.FRowVersion).HasColumnName("F版本号").IsRowVersion();
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FCompanyId).IsUnique().HasDatabaseName("UQ_FIN经营单元_网点公司ID");
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_FIN经营单元_租户ID");
        builder.HasIndex(e => e.FSourceLegacyAuxId).HasDatabaseName("IX_FIN经营单元_来源业务单元ID");
    }
}
