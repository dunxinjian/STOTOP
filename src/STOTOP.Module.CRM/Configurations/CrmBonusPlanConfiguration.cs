using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmBonusPlanConfiguration : IEntityTypeConfiguration<CrmBonusPlan>
{
    public void Configure(EntityTypeBuilder<CrmBonusPlan> builder)
    {
        builder.ToTable("CRM奖金方案");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FPeriod).HasColumnName("F期间").HasMaxLength(10).IsRequired();
        builder.Property(e => e.FTotalAmount).HasColumnName("F奖金总额").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FCalcRules).HasColumnName("F计算规则").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FOaProcessInstanceId).HasColumnName("FOA流程实例ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_CRM奖金方案_F组织ID");

        builder.HasMany(e => e.Details)
            .WithOne(e => e.Plan)
            .HasForeignKey(e => e.FPlanId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
