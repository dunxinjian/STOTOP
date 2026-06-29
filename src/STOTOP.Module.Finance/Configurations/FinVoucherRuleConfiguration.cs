using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinVoucherRuleConfiguration : IEntityTypeConfiguration<FinVoucherRule>
{
    public void Configure(EntityTypeBuilder<FinVoucherRule> builder)
    {
        builder.ToTable("FIN凭证手动规则");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FRuleName).HasColumnName("F规则名称").HasMaxLength(100);
        builder.Property(e => e.FChannelId).HasColumnName("F渠道ID");
        builder.Property(e => e.FMatchCondition).HasColumnName("F匹配条件").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FDebitAccount).HasColumnName("F借方科目").HasMaxLength(50);
        builder.Property(e => e.FCreditAccount).HasColumnName("F贷方科目").HasMaxLength(50);
        builder.Property(e => e.FSummaryTemplate).HasColumnName("F摘要模板").HasMaxLength(200);
        builder.Property(e => e.FPriority).HasColumnName("F优先级").HasDefaultValue(0);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        // 索引
        builder.HasIndex(e => e.FChannelId).HasDatabaseName("IX_FIN凭证手动规则_F渠道ID");
        builder.HasIndex(e => e.FPriority).HasDatabaseName("IX_FIN凭证手动规则_F优先级");

        // 外键: FIN凭证手动规则 -> FIN交易渠道（可空）
        builder.HasOne<FinPaymentChannel>()
            .WithMany()
            .HasForeignKey(e => e.FChannelId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_FIN凭证手动规则_渠道");
    }
}
