using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class PltTenantConfiguration : IEntityTypeConfiguration<PltTenant>
{
    public void Configure(EntityTypeBuilder<PltTenant> builder)
    {
        builder.ToTable("PLT租户");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FCode).HasColumnName("F编号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FRootOrgId).HasColumnName("F根组织ID");
        builder.Property(e => e.FAccountSetBindMode).HasColumnName("F账套绑定模式").HasDefaultValue(1);
        builder.Property(e => e.FDefaultTodoChannel).HasColumnName("F默认待办渠道").HasDefaultValue(1);
        builder.Property(e => e.FPlanId).HasColumnName("F套餐ID");
        builder.Property(e => e.FActivatedAt).HasColumnName("F开通时间");
        builder.Property(e => e.FExpireAt).HasColumnName("F到期时间");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(2);
        builder.Property(e => e.FRowVersion).HasColumnName("F版本号").IsRowVersion();
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FCode).IsUnique().HasDatabaseName("UQ_PLT租户_编号");
        builder.HasIndex(e => e.FRootOrgId).HasDatabaseName("IX_PLT租户_根组织ID");
    }
}
