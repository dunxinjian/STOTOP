using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysRolePermissionConfiguration : IEntityTypeConfiguration<SysRolePermission>
{
    public void Configure(EntityTypeBuilder<SysRolePermission> builder)
    {
        builder.ToTable("SYS角色权限");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FRoleId).HasColumnName("F角色ID");
        builder.Property(e => e.FPermissionId).HasColumnName("F权限ID");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FRoleId, e.FPermissionId }).IsUnique();
        builder.HasIndex(e => e.FPermissionId);

        builder.HasOne(e => e.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(e => e.FRoleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_SYS角色权限_角色");

        builder.HasOne(e => e.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(e => e.FPermissionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_SYS角色权限_权限");
    }
}
