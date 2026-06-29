using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfScheduleConfiguration : IEntityTypeConfiguration<ConfSchedule>
{
    public void Configure(EntityTypeBuilder<ConfSchedule> builder)
    {
        builder.ToTable("CONF日程");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FEventId).HasColumnName("F活动ID");
        builder.Property(e => e.FDate).HasColumnName("F日期").HasColumnType("date");
        builder.Property(e => e.FStartTime).HasColumnName("F开始时间").HasColumnType("time");
        builder.Property(e => e.FEndTime).HasColumnName("F结束时间").HasColumnType("time");
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FLocation).HasColumnName("F地点").HasMaxLength(200);
        builder.Property(e => e.FType).HasColumnName("F类型").HasMaxLength(20);
        builder.Property(e => e.FDescription).HasColumnName("F描述");
        builder.Property(e => e.FSort).HasColumnName("F排序");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FEventId, e.FDate }).HasDatabaseName("IX_CONF日程_活动日期");
    }
}
