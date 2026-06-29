using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmServiceOrderLogConfiguration : IEntityTypeConfiguration<CrmServiceOrderLog>
{
    public void Configure(EntityTypeBuilder<CrmServiceOrderLog> builder)
    {
        builder.ToTable("CRM工单处理记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrderId).HasColumnName("F工单ID");
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FOperationType).HasColumnName("F操作类型");
        builder.Property(e => e.FContent).HasColumnName("F内容").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FAttachments).HasColumnName("F附件").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FOrderId).HasDatabaseName("IX_CRM工单处理记录_F工单ID");
        builder.HasIndex(e => e.FOperatorId).HasDatabaseName("IX_CRM工单处理记录_F操作人ID");
    }
}
