using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfMealAttendeeConfiguration : IEntityTypeConfiguration<ConfMealAttendee>
{
    public void Configure(EntityTypeBuilder<ConfMealAttendee> builder)
    {
        builder.ToTable("CONF餐食人员");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FMealPlanId).HasColumnName("F餐食计划ID");
        builder.Property(e => e.FAttendeeId).HasColumnName("F参会人ID");
        builder.Property(e => e.FDietNote).HasColumnName("F饮食备注").HasMaxLength(100);

        builder.HasOne(e => e.MealPlan)
            .WithMany(e => e.MealAttendees)
            .HasForeignKey(e => e.FMealPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Attendee)
            .WithMany(e => e.MealAttendees)
            .HasForeignKey(e => e.FAttendeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.FMealPlanId, e.FAttendeeId }).IsUnique().HasDatabaseName("IX_CONF餐食人员_唯一");
    }
}
