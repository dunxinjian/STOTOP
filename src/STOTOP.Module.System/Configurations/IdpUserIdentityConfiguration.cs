using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class IdpUserIdentityConfiguration : IEntityTypeConfiguration<IdpUserIdentity>
{
    public void Configure(EntityTypeBuilder<IdpUserIdentity> builder)
    {
        builder.ToTable("IDP用户身份");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FExternalCorpId).HasColumnName("F企业CorpId").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FExternalUserId).HasColumnName("F外部用户ID").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FUnionId).HasColumnName("FUnionId").HasMaxLength(100);
        builder.Property(e => e.FBindStatus).HasColumnName("F绑定状态").HasDefaultValue(1);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FUserId, e.FExternalCorpId }).IsUnique().HasDatabaseName("UQ_IDP用户身份_用户_CorpId");
        builder.HasIndex(e => new { e.FExternalCorpId, e.FExternalUserId }).HasDatabaseName("IX_IDP用户身份_CorpId_外部用户");
    }
}
