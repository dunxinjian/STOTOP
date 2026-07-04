using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class IdpDeptMapConfiguration : IEntityTypeConfiguration<IdpDeptMap>
{
    public void Configure(EntityTypeBuilder<IdpDeptMap> builder)
    {
        builder.ToTable("IDP部门映射");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.Property(e => e.FExternalCorpId).HasColumnName("F企业CorpId").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FExternalDeptId).HasColumnName("F外部部门ID").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FExternalCorpId, e.FExternalDeptId, e.FTenantId }).IsUnique().HasDatabaseName("UQ_IDP部门映射_CorpId_部门_租户");
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_IDP部门映射_租户ID");
    }
}
