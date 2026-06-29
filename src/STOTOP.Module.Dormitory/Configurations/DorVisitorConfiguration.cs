using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Dormitory.Entities;

namespace STOTOP.Module.Dormitory.Configurations;

public class DorVisitorConfiguration : IEntityTypeConfiguration<DorVisitor>
{
    public void Configure(EntityTypeBuilder<DorVisitor> builder)
    {
        builder.ToTable("DOR访客登记");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FRoomId).HasColumnName("F房间ID").IsRequired();
        builder.Property(e => e.FVisitorName).HasColumnName("F访客姓名").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FVisitorPhone).HasColumnName("F访客电话").HasMaxLength(50);
        builder.Property(e => e.FVisitorIdCard).HasColumnName("F访客身份证").HasMaxLength(50);
        builder.Property(e => e.FVisitReason).HasColumnName("F来访事由").HasMaxLength(500);
        builder.Property(e => e.FVisitedPersonId).HasColumnName("F被访人ID");
        builder.Property(e => e.FArrivalTime).HasColumnName("F到访时间").IsRequired();
        builder.Property(e => e.FDepartureTime).HasColumnName("F离开时间");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => e.FRoomId).HasDatabaseName("IX_DOR访客登记_房间ID");
        builder.HasIndex(e => e.FArrivalTime).HasDatabaseName("IX_DOR访客登记_到访时间");

        builder.HasOne(e => e.Room)
            .WithMany()
            .HasForeignKey(e => e.FRoomId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
