using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpPolicyRebateSettlementConfiguration : IEntityTypeConfiguration<ExpPolicyRebateSettlement>
{
    public void Configure(EntityTypeBuilder<ExpPolicyRebateSettlement> builder)
    {
        builder.ToTable("EXP政策返利结算");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FPolicyRebateId).HasColumnName("F政策返利ID");
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.F品牌ID).HasColumnName("F品牌ID");
        builder.Property(e => e.FPeriodStart).HasColumnName("F账期开始");
        builder.Property(e => e.FPeriodEnd).HasColumnName("F账期结束");
        builder.Property(e => e.FTotalWaybills).HasColumnName("F总单量");
        builder.Property(e => e.FTotalWeight).HasColumnName("F总重量").HasColumnType("decimal(14,3)");
        builder.Property(e => e.FAvgWeight).HasColumnName("F平均重量").HasColumnType("decimal(10,3)");
        builder.Property(e => e.FBaseRebateAmount).HasColumnName("F基础返利").HasColumnType("decimal(14,2)");
        builder.Property(e => e.FTotalReward).HasColumnName("F总奖励").HasColumnType("decimal(14,2)");
        builder.Property(e => e.FTotalPenalty).HasColumnName("F总处罚").HasColumnType("decimal(14,2)");
        builder.Property(e => e.FFinalRebateAmount).HasColumnName("F最终返利").HasColumnType("decimal(14,2)");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FConfirmedBy).HasColumnName("F确认人").HasMaxLength(50);
        builder.Property(e => e.FConfirmedTime).HasColumnName("F确认时间");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => new { e.FPolicyRebateId, e.FPeriodStart })
            .HasDatabaseName("IX_EXP政策返利结算_政策账期");
    }
}
