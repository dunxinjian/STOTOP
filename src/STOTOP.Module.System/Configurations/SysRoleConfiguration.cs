using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysRoleConfiguration : IEntityTypeConfiguration<SysRole>
{
    public void Configure(EntityTypeBuilder<SysRole> builder)
    {
        builder.ToTable("SYS角色");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(200);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FScope).HasColumnName("F作用域").HasMaxLength(20).HasDefaultValue(SysRoleScope.Platform);
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.Property(e => e.FIsAdmin).HasColumnName("F是否管理员").HasDefaultValue(false);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FCode).IsUnique();
        // 租户私有角色按租户检索
        builder.HasIndex(e => new { e.FScope, e.FTenantId });
    }
}
