using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfRoomConfiguration : IEntityTypeConfiguration<ConfRoom>
{
    public void Configure(EntityTypeBuilder<ConfRoom> builder)
    {
        builder.ToTable("CONF住宿房间");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FHotelId).HasColumnName("F酒店ID");
        builder.Property(e => e.FRoomNumber).HasColumnName("F房间号").HasMaxLength(20);
        builder.Property(e => e.FRoomType).HasColumnName("F房型").HasMaxLength(20);
        builder.Property(e => e.FCheckInDate).HasColumnName("F入住日期").HasColumnType("date");
        builder.Property(e => e.FCheckOutDate).HasColumnName("F退房日期").HasColumnType("date");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(20).HasDefaultValue("空闲");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(200);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasOne(e => e.Hotel)
            .WithMany(e => e.Rooms)
            .HasForeignKey(e => e.FHotelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.FHotelId).HasDatabaseName("IX_CONF住宿房间_酒店ID");
    }
}
