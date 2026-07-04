using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class IdpTenantCorpMapConfiguration : IEntityTypeConfiguration<IdpTenantCorpMap>
{
    public void Configure(EntityTypeBuilder<IdpTenantCorpMap> builder)
    {
        builder.ToTable("IDP企业租户映射");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.Property(e => e.FExternalCorpId).HasColumnName("F企业CorpId").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FTenantId, e.FExternalCorpId }).IsUnique().HasDatabaseName("UQ_IDP企业租户映射_租户_CorpId");
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_IDP企业租户映射_租户ID");
    }
}
