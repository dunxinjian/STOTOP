using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysUserNetworkPermissionConfiguration : IEntityTypeConfiguration<SysUserNetworkPermission>
{
    public void Configure(EntityTypeBuilder<SysUserNetworkPermission> builder)
    {
        builder.ToTable("SYS用户网点权限");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FNetworkPointCode).HasColumnName("F网点编号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FPermissionType).HasColumnName("F权限类型").HasDefaultValue(1);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => new { e.FUserId, e.FNetworkPointCode }).IsUnique()
            .HasDatabaseName("UQ_SYS用户网点权限_用户网点");
    }
}
