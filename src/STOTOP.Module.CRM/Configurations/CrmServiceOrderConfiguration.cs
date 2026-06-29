using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmServiceOrderConfiguration : IEntityTypeConfiguration<CrmServiceOrder>
{
    public void Configure(EntityTypeBuilder<CrmServiceOrder> builder)
    {
        builder.ToTable("CRM服务工单");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrderNo).HasColumnName("F工单号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FCustomerId).HasColumnName("F客户ID").HasMaxLength(50);
        builder.Property(e => e.FAssigneeId).HasColumnName("F受理人ID");
        builder.Property(e => e.FCategory).HasColumnName("F分类");
        builder.Property(e => e.FPriority).HasColumnName("F优先级").HasDefaultValue(3);
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FResolvedTime).HasColumnName("F解决时间");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FOrderNo).IsUnique().HasDatabaseName("UX_CRM服务工单_F工单号");
        builder.HasIndex(e => e.FCustomerId).HasDatabaseName("IX_CRM服务工单_F客户ID");
        builder.HasIndex(e => e.FAssigneeId).HasDatabaseName("IX_CRM服务工单_F受理人ID");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_CRM服务工单_F状态");

        builder.HasMany(e => e.Logs)
            .WithOne(e => e.Order)
            .HasForeignKey(e => e.FOrderId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
