using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Dormitory.Entities;

namespace STOTOP.Module.Dormitory.Configurations;

public class DorRepairOrderConfiguration : IEntityTypeConfiguration<DorRepairOrder>
{
    public void Configure(EntityTypeBuilder<DorRepairOrder> builder)
    {
        builder.ToTable("DOR报修工单");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FRoomId).HasColumnName("F房间ID").IsRequired();
        builder.Property(e => e.FReporterId).HasColumnName("F报修人ID").IsRequired();
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.FPriority).HasColumnName("F紧急程度").HasDefaultValue(1);
        builder.Property(e => e.FHandlerId).HasColumnName("F处理人ID");
        builder.Property(e => e.FResult).HasColumnName("F处理结果").HasMaxLength(1000);
        builder.Property(e => e.FHandledTime).HasColumnName("F处理时间");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FRoomId).HasDatabaseName("IX_DOR报修工单_房间ID");
        builder.HasIndex(e => e.FReporterId).HasDatabaseName("IX_DOR报修工单_报修人ID");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_DOR报修工单_状态");

        builder.HasOne(e => e.Room)
            .WithMany()
            .HasForeignKey(e => e.FRoomId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
