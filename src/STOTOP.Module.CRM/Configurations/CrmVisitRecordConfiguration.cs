using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmVisitRecordConfiguration : IEntityTypeConfiguration<CrmVisitRecord>
{
    public void Configure(EntityTypeBuilder<CrmVisitRecord> builder)
    {
        builder.ToTable("CRM拜访记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCustomerId).HasColumnName("F客户ID").HasMaxLength(50);
        builder.Property(e => e.FVisitorId).HasColumnName("F拜访人ID");
        builder.Property(e => e.FVisitDate).HasColumnName("F拜访日期");
        builder.Property(e => e.FVisitMethod).HasColumnName("F拜访方式");
        builder.Property(e => e.FContent).HasColumnName("F内容").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FNextFollowUpDate).HasColumnName("F下次跟进日期");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FCustomerId).HasDatabaseName("IX_CRM拜访记录_F客户ID");
        builder.HasIndex(e => e.FVisitorId).HasDatabaseName("IX_CRM拜访记录_F拜访人ID");
        builder.HasIndex(e => e.FVisitDate).HasDatabaseName("IX_CRM拜访记录_F拜访日期");
        builder.HasIndex(e => e.FNextFollowUpDate).HasDatabaseName("IX_CRM拜访记录_F下次跟进日期");
    }
}
