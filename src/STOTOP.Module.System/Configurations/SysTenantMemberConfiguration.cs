using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysTenantMemberConfiguration : IEntityTypeConfiguration<SysTenantMember>
{
    public void Configure(EntityTypeBuilder<SysTenantMember> builder)
    {
        builder.ToTable("SYS租户成员");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID");
        builder.Property(e => e.FIsPrimary).HasColumnName("F是否主租户").HasDefaultValue(false);
        builder.Property(e => e.FInviteStatus).HasColumnName("F邀请状态").HasDefaultValue(2);
        builder.Property(e => e.FInvitedBy).HasColumnName("F邀请人");
        builder.Property(e => e.FJoinedAt).HasColumnName("F加入时间");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FUserId, e.FTenantId })
            .IsUnique()
            .HasDatabaseName("UQ_SYS租户成员_用户_租户");
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_SYS租户成员_租户ID");
    }
}
