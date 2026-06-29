using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfScheduleAttendeeConfiguration : IEntityTypeConfiguration<ConfScheduleAttendee>
{
    public void Configure(EntityTypeBuilder<ConfScheduleAttendee> builder)
    {
        builder.ToTable("CONF日程参会人");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FScheduleId).HasColumnName("F日程ID");
        builder.Property(e => e.FAttendeeId).HasColumnName("F参会人ID");

        builder.HasOne(e => e.Schedule)
            .WithMany(e => e.ScheduleAttendees)
            .HasForeignKey(e => e.FScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Attendee)
            .WithMany(e => e.ScheduleAttendees)
            .HasForeignKey(e => e.FAttendeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.FScheduleId, e.FAttendeeId }).IsUnique().HasDatabaseName("IX_CONF日程参会人_唯一");
    }
}
