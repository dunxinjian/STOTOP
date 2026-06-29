using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysCodeRuleConfiguration : IEntityTypeConfiguration<SysCodeRule>
{
    public void Configure(EntityTypeBuilder<SysCodeRule> builder)
    {
        builder.ToTable("SYS编码规则");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FRuleCode).HasColumnName("F规则编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FRuleName).HasColumnName("F规则名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FBusinessEntity).HasColumnName("F业务实体").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FCodeField).HasColumnName("F编码字段").HasMaxLength(50).IsRequired().HasDefaultValue("F编码");
        builder.Property(e => e.FPrefix).HasColumnName("F前缀").HasMaxLength(20);
        builder.Property(e => e.FDateFormat).HasColumnName("F日期格式").HasMaxLength(20);
        builder.Property(e => e.FSeqLength).HasColumnName("F流水号长度").HasDefaultValue(4);
        builder.Property(e => e.FSeparator).HasColumnName("F分隔符").HasMaxLength(5).HasDefaultValue("-");
        builder.Property(e => e.FResetCycle).HasColumnName("F重置周期").HasMaxLength(10).IsRequired().HasDefaultValue("never");
        builder.Property(e => e.FOrgIsolation).HasColumnName("F组织隔离").HasDefaultValue(false);
        builder.Property(e => e.FEnabled).HasColumnName("F启用").HasDefaultValue(true);
        builder.Property(e => e.FDescription).HasColumnName("F说明").HasMaxLength(200);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F修改时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FRuleCode).IsUnique();
    }
}
