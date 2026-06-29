using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfPickupPassengerConfiguration : IEntityTypeConfiguration<ConfPickupPassenger>
{
    public void Configure(EntityTypeBuilder<ConfPickupPassenger> builder)
    {
        builder.ToTable("CONF接送乘客");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FPickupTaskId).HasColumnName("F接送任务ID");
        builder.Property(e => e.FAttendeeId).HasColumnName("F参会人ID");

        builder.HasOne(e => e.PickupTask)
            .WithMany(e => e.Passengers)
            .HasForeignKey(e => e.FPickupTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Attendee)
            .WithMany(e => e.PickupPassengers)
            .HasForeignKey(e => e.FAttendeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.FPickupTaskId, e.FAttendeeId }).IsUnique().HasDatabaseName("IX_CONF接送乘客_唯一");
    }
}
