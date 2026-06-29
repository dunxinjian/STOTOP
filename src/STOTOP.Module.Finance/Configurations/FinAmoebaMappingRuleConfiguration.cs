using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAmoebaMappingRuleConfiguration : IEntityTypeConfiguration<FinAmoebaMappingRule>
{
    public void Configure(EntityTypeBuilder<FinAmoebaMappingRule> builder)
    {
        builder.ToTable("FIN阿米巴映射规则");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUnitId).HasColumnName("F经营单元ID");
        builder.Property(e => e.FDataSourceType).HasColumnName("F数据源类型");
        builder.Property(e => e.FSiteCode).HasColumnName("F网点编号").HasMaxLength(50);
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasMaxLength(2).IsFixedLength();
        builder.Property(e => e.FDirection).HasColumnName("F业务方向").HasMaxLength(10);
        builder.Property(e => e.FAuxField).HasColumnName("F辅助匹配字段").HasMaxLength(100);
        builder.Property(e => e.FAuxValue).HasColumnName("F辅助匹配值").HasMaxLength(200);
        builder.Property(e => e.FPriority).HasColumnName("F优先级").HasDefaultValue(0);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(192L);
        
        builder.HasIndex(e => e.FUnitId).HasDatabaseName("IX_FIN阿米巴映射规则_经营单元");
        builder.HasIndex(e => e.FDataSourceType).HasDatabaseName("IX_FIN阿米巴映射规则_数据源");
    }
}
