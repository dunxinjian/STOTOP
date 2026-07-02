using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysOrganizationConfiguration : IEntityTypeConfiguration<SysOrganization>
{
    public void Configure(EntityTypeBuilder<SysOrganization> builder)
    {
        builder.ToTable("SYS组织架构");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(64).IsRequired();
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FParentId).HasColumnName("F父ID").HasDefaultValue(0);
        builder.Property(e => e.FTypeId).HasColumnName("F类型ID").HasDefaultValue(5L);
#pragma warning disable CS0618 // 充许访问 Obsolete 成员进行列映射
        builder.Property(e => e.FType).HasColumnName("F类型").HasMaxLength(20).HasDefaultValue("部门");
#pragma warning restore CS0618
        builder.Property(e => e.FSort).HasColumnName("F排序").HasDefaultValue(0);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FDingTalkDeptId).HasColumnName("F钉钉部门ID").HasMaxLength(100);
        builder.Property(e => e.FDingTalkBindStatus).HasColumnName("F钉钉绑定状态").HasDefaultValue(0);
        builder.Property(e => e.FDingTalkDeptName).HasColumnName("F钉钉部门名称").HasMaxLength(200);
        builder.Property(e => e.FManagerId).HasColumnName("F负责人ID");
        builder.Property(e => e.FHeadcount).HasColumnName("F编制人数").HasDefaultValue(0);
        builder.Property(e => e.FIsSwitchable).HasColumnName("F可切换").HasDefaultValue(false);
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(500);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        // ===== M4 多租户阶段2 组织模型列 =====
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.Property(e => e.FKind).HasColumnName("F组织类别").HasDefaultValue((int)Entities.OrgKind.Dept);
        builder.Property(e => e.FParentKind).HasColumnName("F父类别");
        builder.Property(e => e.FCompanyId).HasColumnName("F所属网点公司ID");
        builder.Property(e => e.FScopeRootId).HasColumnName("F范围根ID").HasDefaultValue(0L);
        builder.Property(e => e.FScopeRootType).HasColumnName("F范围根类型").HasDefaultValue((int)Entities.OrgScopeType.Group);
        builder.Property(e => e.FPath).HasColumnName("F路径").HasMaxLength(400);
        builder.Property(e => e.FRowVersion).HasColumnName("F版本号").IsRowVersion();

        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => e.FCode).IsUnique();
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_SYS组织架构_租户ID");
        builder.HasIndex(e => e.FKind).HasDatabaseName("IX_SYS组织架构_组织类别");
        builder.HasIndex(e => e.FParentId).HasDatabaseName("IX_SYS组织架构_父ID");
        builder.HasIndex(e => e.FScopeRootId).HasDatabaseName("IX_SYS组织架构_范围根ID");

        builder.HasOne(e => e.OrgType)
            .WithMany()
            .HasForeignKey(e => e.FTypeId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_SYS组织架构_类型");

        builder.HasOne(e => e.Manager)
            .WithMany()
            .HasForeignKey(e => e.FManagerId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_SYS组织架构_负责人");
    }
}
