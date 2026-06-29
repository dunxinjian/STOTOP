using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Configurations;

public class QlRuleConditionConfiguration : IEntityTypeConfiguration<QlRuleCondition>
{
    public void Configure(EntityTypeBuilder<QlRuleCondition> builder)
    {
        builder.ToTable("QL规则条件");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FRuleId).HasColumnName("F规则ID");
        builder.Property(e => e.FFieldName).HasColumnName("F字段名").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FOperator).HasColumnName("F运算符").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FThreshold).HasColumnName("F阈值").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FLogicRelation).HasColumnName("F逻辑关系").HasMaxLength(10).IsRequired();
        builder.Property(e => e.FSort).HasColumnName("F排序");

        builder.HasIndex(e => e.FRuleId).HasDatabaseName("IX_QL规则条件_规则ID");
    }
}
