using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinBudgetOccupationConfiguration : IEntityTypeConfiguration<FinBudgetOccupation>
{
    public void Configure(EntityTypeBuilder<FinBudgetOccupation> builder)
    {
        builder.ToTable("FIN预算占用");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBudgetVersionId).HasColumnName("F预算版本ID");
        builder.Property(e => e.FBudgetLineId).HasColumnName("F预算明细ID");
        builder.Property(e => e.FSourceType).HasColumnName("F来源类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FSourceId).HasColumnName("F来源ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FPeriod).HasColumnName("F期间").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FAccountCode).HasColumnName("F科目编码").HasMaxLength(50);
        builder.Property(e => e.FPLItemId).HasColumnName("F损益项ID");
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(30).HasDefaultValue("occupied");
        builder.Property(e => e.FTransitionKey).HasColumnName("F转换键").HasMaxLength(200);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FBudgetVersionId, e.FSourceType, e.FSourceId, e.FPeriod, e.FAccountCode, e.FPLItemId })
            .HasDatabaseName("IX_FIN预算占用_预算来源期间科目损益项");
    }
}
