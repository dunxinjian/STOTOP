using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysUserOrganizationConfiguration : IEntityTypeConfiguration<SysUserOrganization>
{
    public void Configure(EntityTypeBuilder<SysUserOrganization> builder)
    {
        builder.ToTable("SYS用户组织");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FDirectSuperiorId).HasColumnName("F直接上级ID");
        builder.Property(e => e.FIsPrimaryOrg).HasColumnName("F是否主组织").HasDefaultValue(0);
        builder.Property(e => e.FPosition).HasColumnName("F职位").HasMaxLength(200);
        builder.Property(e => e.FJobNumber).HasColumnName("F工号").HasMaxLength(50);
        builder.Property(e => e.FEntryDate).HasColumnName("F入职日期");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.F生效起期).HasColumnName("F生效起期").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.F生效止期).HasColumnName("F生效止期");
        builder.Property(e => e.F是否当前).HasColumnName("F是否当前").HasDefaultValue(true);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.SysUserFID).HasColumnName("SysUserFID");

        // 旧唯一索引 UQ_SYS用户组织(FUserId, FOrgId) 已由 SystemSeeder V3 在数据库层 DROP，
        // 此处不再声明 IsUnique，避免 EF Model 与实际 schema 不一致。
        // 新的 filtered 唯一索引：仅在 F是否当前 = 1 的记录上限制 (FOrgId, FUserId) 唯一。
        builder.HasIndex(e => new { e.FOrgId, e.FUserId })
            .IsUnique()
            .HasFilter("[F是否当前] = 1")
            .HasDatabaseName("UQ_SysUserOrganization_组织_用户_当前");

        builder.HasOne(e => e.User)
            .WithMany(u => u.UserOrganizations)
            .HasForeignKey(e => e.FUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_用户组织_用户");

        builder.HasOne(e => e.Organization)
            .WithMany(o => o.UserOrganizations)
            .HasForeignKey(e => e.FOrgId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_用户组织_组织");

        builder.HasOne(e => e.DirectSuperior)
            .WithMany()
            .HasForeignKey(e => e.FDirectSuperiorId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_用户组织_上级");
    }
}
