using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysUserRoleConfiguration : IEntityTypeConfiguration<SysUserRole>
{
    public void Configure(EntityTypeBuilder<SysUserRole> builder)
    {
        builder.ToTable("SYS用户角色");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FRoleId).HasColumnName("F角色ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.SysUserFID).HasColumnName("SysUserFID");

        builder.HasIndex(e => new { e.FUserId, e.FRoleId }).IsUnique();
        builder.HasIndex(e => e.FRoleId);

        builder.HasOne(e => e.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(e => e.FUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_SYS用户角色_用户");

        builder.HasOne(e => e.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(e => e.FRoleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_SYS用户角色_角色");

        builder.HasOne(e => e.Organization)
            .WithMany()
            .HasForeignKey(e => e.FOrgId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_SYS用户角色_组织");
    }
}
