using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysUserConfiguration : IEntityTypeConfiguration<SysUser>
{
    public void Configure(EntityTypeBuilder<SysUser> builder)
    {
        builder.ToTable("SYS用户");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(64).IsRequired();
        builder.Property(e => e.FName).HasColumnName("F姓名").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FAccount).HasColumnName("F账号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FPhone).HasColumnName("F手机号").HasMaxLength(20);
        builder.Property(e => e.FEmail).HasColumnName("F邮箱").HasMaxLength(100);
        builder.Property(e => e.FPasswordHash).HasColumnName("F密码哈希").HasMaxLength(256).IsRequired();
        builder.Property(e => e.FAvatar).HasColumnName("F头像").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FDingTalkUserId).HasColumnName("F钉钉用户ID").HasMaxLength(100);
        builder.Property(e => e.FDingTalkBindStatus).HasColumnName("F钉钉绑定状态").HasDefaultValue(0);
        builder.Property(e => e.FDingTalkUserName).HasColumnName("F钉钉用户名").HasMaxLength(100);
        builder.Property(e => e.FDingTalkUnionId).HasColumnName("F钉钉UnionId").HasMaxLength(100);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.F布局偏好).HasColumnName("F布局偏好");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => e.FAccount).IsUnique();
        builder.HasIndex(e => e.FPhone).IsUnique().HasFilter("[F手机号] IS NOT NULL");
    }
}
