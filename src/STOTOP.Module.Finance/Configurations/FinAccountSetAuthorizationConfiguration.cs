using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAccountSetAuthorizationConfiguration : IEntityTypeConfiguration<FinAccountSetAuthorization>
{
    public void Configure(EntityTypeBuilder<FinAccountSetAuthorization> builder)
    {
        builder.ToTable("FIN账套授权");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FAccountSetRoleId).HasColumnName("F账套角色ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FGrantedBy).HasColumnName("F授权人ID").HasDefaultValue(0L);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FUserId, e.FAccountSetId }).IsUnique().HasDatabaseName("UK_FIN账套授权_用户账套");
        builder.HasIndex(e => e.FAccountSetRoleId).HasDatabaseName("IX_FIN账套授权_角色ID");
    }
}
