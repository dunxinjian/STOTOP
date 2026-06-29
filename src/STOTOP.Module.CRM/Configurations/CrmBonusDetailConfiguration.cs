using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmBonusDetailConfiguration : IEntityTypeConfiguration<CrmBonusDetail>
{
    public void Configure(EntityTypeBuilder<CrmBonusDetail> builder)
    {
        builder.ToTable("CRM奖金明细");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FPlanId).HasColumnName("F方案ID");
        builder.Property(e => e.FEmployeeId).HasColumnName("F员工ID");
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FBonusType).HasColumnName("F奖金类型");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FPlanId).HasDatabaseName("IX_CRM奖金明细_F方案ID");
        builder.HasIndex(e => e.FEmployeeId).HasDatabaseName("IX_CRM奖金明细_F员工ID");
    }
}
