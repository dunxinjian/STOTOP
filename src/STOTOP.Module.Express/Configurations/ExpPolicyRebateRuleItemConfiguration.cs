using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpPolicyRebateRuleItemConfiguration : IEntityTypeConfiguration<ExpPolicyRebateRuleItem>
{
    public void Configure(EntityTypeBuilder<ExpPolicyRebateRuleItem> builder)
    {
        builder.ToTable("EXP政策返利规则条件");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FRuleId).HasColumnName("F规则ID");
        builder.Property(e => e.FThresholdLower).HasColumnName("F阈值下限").HasColumnType("decimal(10,4)");
        builder.Property(e => e.FThresholdUpper).HasColumnName("F阈值上限").HasColumnType("decimal(10,4)");
        builder.Property(e => e.FWeightFrom).HasColumnName("F起始重量").HasColumnType("decimal(10,3)");
        builder.Property(e => e.FWeightTo).HasColumnName("F截止重量").HasColumnType("decimal(10,3)");
        builder.Property(e => e.FProvinceId).HasColumnName("F省份ID");
        builder.Property(e => e.FAdjustType).HasColumnName("F调整方向");
        builder.Property(e => e.FAdjustCalcMethod).HasColumnName("F调整计算方式");
        builder.Property(e => e.FAdjustAmount).HasColumnName("F调整金额").HasColumnType("decimal(10,4)");
        builder.Property(e => e.FAdjustRate).HasColumnName("F调整比例").HasColumnType("decimal(5,4)");
        builder.Property(e => e.FSortOrder).HasColumnName("F排序").HasDefaultValue(0);

        builder.HasIndex(e => new { e.FRuleId, e.FSortOrder })
            .HasDatabaseName("IX_EXP政策返利规则条件_规则排序");
    }
}
