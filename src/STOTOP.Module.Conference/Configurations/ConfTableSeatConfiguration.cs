using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfTableSeatConfiguration : IEntityTypeConfiguration<ConfTableSeat>
{
    public void Configure(EntityTypeBuilder<ConfTableSeat> builder)
    {
        builder.ToTable("CONF桌次座位");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTableId).HasColumnName("F桌次ID");
        builder.Property(e => e.FAttendeeId).HasColumnName("F参会人ID");
        builder.Property(e => e.FSeatNumber).HasColumnName("F座位号");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(100);

        builder.HasOne(e => e.Table)
            .WithMany(e => e.Seats)
            .HasForeignKey(e => e.FTableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Attendee)
            .WithMany(e => e.TableSeats)
            .HasForeignKey(e => e.FAttendeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.FTableId, e.FAttendeeId }).IsUnique().HasDatabaseName("IX_CONF桌次座位_唯一");
    }
}
