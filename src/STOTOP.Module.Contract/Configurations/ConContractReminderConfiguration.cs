using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Contract.Entities;

namespace STOTOP.Module.Contract.Configurations;

public class ConContractReminderConfiguration : IEntityTypeConfiguration<ConContractReminder>
{
    public void Configure(EntityTypeBuilder<ConContractReminder> builder)
    {
        builder.ToTable("CON合同提醒");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FContractId).HasColumnName("F合同ID");
        builder.Property(e => e.FReminderType).HasColumnName("F提醒类型");
        builder.Property(e => e.FReminderDate).HasColumnName("F提醒日期");
        builder.Property(e => e.FRecipientId).HasColumnName("F接收人ID");
        builder.Property(e => e.FIsHandled).HasColumnName("F是否已处理").HasDefaultValue(false);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FContractId).HasDatabaseName("IX_CON合同提醒_F合同ID");
        builder.HasIndex(e => e.FRecipientId).HasDatabaseName("IX_CON合同提醒_F接收人ID");
        builder.HasIndex(e => e.FReminderDate).HasDatabaseName("IX_CON合同提醒_F提醒日期");
    }
}
