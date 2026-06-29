using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpPolicyRebateSettlementDetailConfiguration : IEntityTypeConfiguration<ExpPolicyRebateSettlementDetail>
{
    public void Configure(EntityTypeBuilder<ExpPolicyRebateSettlementDetail> builder)
    {
        builder.ToTable("EXP政策返利结算明细");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FSettlementId).HasColumnName("F结算ID");
        builder.Property(e => e.FRuleId).HasColumnName("F规则ID");
        builder.Property(e => e.FRuleItemId).HasColumnName("F规则条件ID");
        builder.Property(e => e.FActualValue).HasColumnName("F实际值").HasColumnType("decimal(14,4)");
        builder.Property(e => e.FThresholdValue).HasColumnName("F阈值").HasColumnType("decimal(14,4)");
        builder.Property(e => e.FAdjustType).HasColumnName("F调整方向");
        builder.Property(e => e.FAdjustAmount).HasColumnName("F调整金额").HasColumnType("decimal(12,2)");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(200);
    }
}
