using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Dormitory.Entities;

namespace STOTOP.Module.Dormitory.Configurations;

public class DorHygieneCheckConfiguration : IEntityTypeConfiguration<DorHygieneCheck>
{
    public void Configure(EntityTypeBuilder<DorHygieneCheck> builder)
    {
        builder.ToTable("DOR卫生检查");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FRoomId).HasColumnName("F房间ID").IsRequired();
        builder.Property(e => e.FInspectorId).HasColumnName("F检查人ID").IsRequired();
        builder.Property(e => e.FCheckDate).HasColumnName("F检查日期").IsRequired();
        builder.Property(e => e.FScore).HasColumnName("F评分");
        builder.Property(e => e.FResult).HasColumnName("F检查结果").HasMaxLength(50);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => e.FRoomId).HasDatabaseName("IX_DOR卫生检查_房间ID");
        builder.HasIndex(e => e.FCheckDate).HasDatabaseName("IX_DOR卫生检查_检查日期");

        builder.HasOne(e => e.Room)
            .WithMany()
            .HasForeignKey(e => e.FRoomId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
