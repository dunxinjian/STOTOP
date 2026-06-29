using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfRoomGuestConfiguration : IEntityTypeConfiguration<ConfRoomGuest>
{
    public void Configure(EntityTypeBuilder<ConfRoomGuest> builder)
    {
        builder.ToTable("CONF房间入住");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FRoomId).HasColumnName("F房间ID");
        builder.Property(e => e.FAttendeeId).HasColumnName("F参会人ID");

        builder.HasOne(e => e.Room)
            .WithMany(e => e.RoomGuests)
            .HasForeignKey(e => e.FRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Attendee)
            .WithMany(e => e.RoomGuests)
            .HasForeignKey(e => e.FAttendeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.FRoomId, e.FAttendeeId }).IsUnique().HasDatabaseName("IX_CONF房间入住_唯一");
    }
}
