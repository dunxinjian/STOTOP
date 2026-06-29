using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmReferralConfiguration : IEntityTypeConfiguration<CrmReferral>
{
    public void Configure(EntityTypeBuilder<CrmReferral> builder)
    {
        builder.ToTable("CRM推荐记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCustomerId).HasColumnName("F客户ID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FReferrerType).HasColumnName("F推荐人类型");
        builder.Property(e => e.FEmployeeId).HasColumnName("F员工ID");
        builder.Property(e => e.FExternalContactId).HasColumnName("F外部联系人ID");
        builder.Property(e => e.FReferralDate).HasColumnName("F推荐日期");
        builder.Property(e => e.FDescription).HasColumnName("F说明").HasMaxLength(500);
        builder.Property(e => e.FCommissionRate).HasColumnName("F返佣比例").HasColumnType("decimal(8,4)");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FCustomerId).HasDatabaseName("IX_CRM推荐记录_F客户ID");
        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_CRM推荐记录_F组织ID");
        builder.HasIndex(e => e.FEmployeeId).HasDatabaseName("IX_CRM推荐记录_F员工ID");
        builder.HasIndex(e => e.FExternalContactId).HasDatabaseName("IX_CRM推荐记录_F外部联系人ID");

        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.FCustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ExternalContact)
            .WithMany(e => e.Referrals)
            .HasForeignKey(e => e.FExternalContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Commissions)
            .WithOne(e => e.Referral)
            .HasForeignKey(e => e.FReferralId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
