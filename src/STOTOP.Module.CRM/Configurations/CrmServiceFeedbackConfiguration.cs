using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmServiceFeedbackConfiguration : IEntityTypeConfiguration<CrmServiceFeedback>
{
    public void Configure(EntityTypeBuilder<CrmServiceFeedback> builder)
    {
        builder.ToTable("CRM服务反馈");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FSubmitterId).HasColumnName("F提交人ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCustomerId).HasColumnName("F客户ID").HasMaxLength(50);
        builder.Property(e => e.FOrderId).HasColumnName("F工单ID");
        builder.Property(e => e.FCategory).HasColumnName("F分类");
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FSuggestion).HasColumnName("F改善建议").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FAttachments).HasColumnName("F附件").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FHandlerId).HasColumnName("F处理人ID");
        builder.Property(e => e.FHandleResult).HasColumnName("F处理结果").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FSubmitterId).HasDatabaseName("IX_CRM服务反馈_F提交人ID");
        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_CRM服务反馈_F组织ID");
        builder.HasIndex(e => e.FCustomerId).HasDatabaseName("IX_CRM服务反馈_F客户ID");
        builder.HasIndex(e => e.FOrderId).HasDatabaseName("IX_CRM服务反馈_F工单ID");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_CRM服务反馈_F状态");
    }
}
