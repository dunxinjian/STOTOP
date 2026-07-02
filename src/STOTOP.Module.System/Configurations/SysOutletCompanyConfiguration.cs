using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysOutletCompanyConfiguration : IEntityTypeConfiguration<SysOutletCompany>
{
    public void Configure(EntityTypeBuilder<SysOutletCompany> builder)
    {
        builder.ToTable("SYS网点公司");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.Property(e => e.FOrgNodeId).HasColumnName("F组织节点ID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FCreditCode).HasColumnName("F统一社会信用代码").HasMaxLength(50);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FRowVersion).HasColumnName("F版本号").IsRowVersion();
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        // 与组织节点 1:1
        builder.HasIndex(e => e.FOrgNodeId).IsUnique().HasDatabaseName("UQ_SYS网点公司_组织节点ID");
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_SYS网点公司_租户ID");
    }
}
