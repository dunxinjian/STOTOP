using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysPermissionConfiguration : IEntityTypeConfiguration<SysPermission>
{
    public void Configure(EntityTypeBuilder<SysPermission> builder)
    {
        builder.ToTable("SYS功能权限");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FType).HasColumnName("F类型").HasMaxLength(20).HasDefaultValue("菜单");
        builder.Property(e => e.FParentId).HasColumnName("F父ID").HasDefaultValue(0);
        builder.Property(e => e.FRoute).HasColumnName("F路由").HasMaxLength(200);
        builder.Property(e => e.FComponentPath).HasColumnName("F组件路径").HasMaxLength(200);
        builder.Property(e => e.FIcon).HasColumnName("F图标").HasMaxLength(100);
        builder.Property(e => e.FSort).HasColumnName("F排序").HasDefaultValue(0);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FIsVisible).HasColumnName("F是否可见").HasDefaultValue(1);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FCode).IsUnique();
        builder.HasIndex(e => e.FParentId);
    }
}
