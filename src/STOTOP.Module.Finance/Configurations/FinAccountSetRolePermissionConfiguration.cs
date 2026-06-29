using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAccountSetRolePermissionConfiguration : IEntityTypeConfiguration<FinAccountSetRolePermission>
{
    public void Configure(EntityTypeBuilder<FinAccountSetRolePermission> builder)
    {
        builder.ToTable("FIN账套角色权限");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FAccountSetRoleId).HasColumnName("F账套角色ID");
        builder.Property(e => e.FPermissionCode).HasColumnName("F权限编码").HasMaxLength(100);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => e.FAccountSetRoleId).HasDatabaseName("IX_FIN账套角色权限_角色ID");
    }
}
