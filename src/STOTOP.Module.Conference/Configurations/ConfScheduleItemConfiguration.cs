using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfScheduleItemConfiguration : IEntityTypeConfiguration<ConfScheduleItem>
{
    public void Configure(EntityTypeBuilder<ConfScheduleItem> builder)
    {
        builder.ToTable("CONF日程物品");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FScheduleId).HasColumnName("F日程ID");
        builder.Property(e => e.FItemName).HasColumnName("F物品名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FQuantity).HasColumnName("F数量");
        builder.Property(e => e.FUnit).HasColumnName("F单位").HasMaxLength(20);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(20);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(200);

        builder.HasOne(e => e.Schedule)
            .WithMany(e => e.ScheduleItems)
            .HasForeignKey(e => e.FScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
