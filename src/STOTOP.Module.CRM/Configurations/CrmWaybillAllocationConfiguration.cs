using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmWaybillAllocationConfiguration : IEntityTypeConfiguration<CrmWaybillAllocation>
{
    public void Configure(EntityTypeBuilder<CrmWaybillAllocation> builder)
    {
        builder.ToTable("CRM运单号发放");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FPrepaymentId).HasColumnName("F预付款ID");
        builder.Property(e => e.FCustomerId).HasColumnName("F客户ID").HasMaxLength(50);
        builder.Property(e => e.FPoolId).HasColumnName("F号段池ID");
        builder.Property(e => e.FStartNo).HasColumnName("F发放起始号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FEndNo).HasColumnName("F发放结束号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FAllocatedCount).HasColumnName("F发放数量").HasDefaultValue(0);
        builder.Property(e => e.FAllocationDate).HasColumnName("F发放日期");
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FPrepaymentId).HasDatabaseName("IX_CRM运单号发放_F预付款ID");
        builder.HasIndex(e => e.FCustomerId).HasDatabaseName("IX_CRM运单号发放_F客户ID");
        builder.HasIndex(e => e.FPoolId).HasDatabaseName("IX_CRM运单号发放_F号段池ID");
        builder.HasIndex(e => e.FOperatorId).HasDatabaseName("IX_CRM运单号发放_F操作人ID");

        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.FCustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Pool)
            .WithMany(e => e.Allocations)
            .HasForeignKey(e => e.FPoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
