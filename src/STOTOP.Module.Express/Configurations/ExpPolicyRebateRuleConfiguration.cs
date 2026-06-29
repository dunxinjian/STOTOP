using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpPolicyRebateRuleConfiguration : IEntityTypeConfiguration<ExpPolicyRebateRule>
{
    public void Configure(EntityTypeBuilder<ExpPolicyRebateRule> builder)
    {
        builder.ToTable("EXP政策返利奖罚规则");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FPolicyRebateId).HasColumnName("F政策返利ID");
        builder.Property(e => e.FRuleType).HasColumnName("F规则类型");
        builder.Property(e => e.FRuleName).HasColumnName("F规则名称").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FEnabled).HasColumnName("F启用").HasDefaultValue(true);
        builder.Property(e => e.FSortOrder).HasColumnName("F排序").HasDefaultValue(0);

        builder.HasIndex(e => new { e.FPolicyRebateId, e.FSortOrder })
            .HasDatabaseName("IX_EXP政策返利奖罚规则_政策排序");
    }
}
