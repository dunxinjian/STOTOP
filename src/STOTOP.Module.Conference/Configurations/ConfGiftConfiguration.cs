using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfGiftConfiguration : IEntityTypeConfiguration<ConfGift>
{
    public void Configure(EntityTypeBuilder<ConfGift> builder)
    {
        builder.ToTable("CONF礼金");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FEventId).HasColumnName("F活动ID");
        builder.Property(e => e.FAttendeeId).HasColumnName("F宾客ID");
        builder.Property(e => e.FGuestName).HasColumnName("F宾客姓名").HasMaxLength(50);
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FGiftDescription).HasColumnName("F礼物描述");
        builder.Property(e => e.FRegistrationTime).HasColumnName("F登记时间");
        builder.Property(e => e.FRegistrationMethod).HasColumnName("F登记方式").HasMaxLength(20).HasDefaultValue("现金");
        builder.Property(e => e.FIsReturned).HasColumnName("F是否已回礼");
        builder.Property(e => e.FReturnContent).HasColumnName("F回礼内容");
        builder.Property(e => e.FReturnTime).HasColumnName("F回礼时间");
        builder.Property(e => e.FRemark).HasColumnName("F备注");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasOne(e => e.Event)
            .WithMany(ev => ev.Gifts)
            .HasForeignKey(e => e.FEventId);

        builder.HasOne(e => e.Attendee)
            .WithMany(a => a.Gifts)
            .HasForeignKey(e => e.FAttendeeId)
            .IsRequired(false);

        builder.HasIndex(e => e.FEventId).HasDatabaseName("IX_CONF礼金_活动ID");
        builder.HasIndex(e => e.FAttendeeId).HasDatabaseName("IX_CONF礼金_宾客ID");
    }
}
