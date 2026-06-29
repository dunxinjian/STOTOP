using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfHotelConfiguration : IEntityTypeConfiguration<ConfHotel>
{
    public void Configure(EntityTypeBuilder<ConfHotel> builder)
    {
        builder.ToTable("CONF住宿酒店");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FEventId).HasColumnName("F活动ID");
        builder.Property(e => e.FHotelName).HasColumnName("F酒店名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FAddress).HasColumnName("F地址").HasMaxLength(300);
        builder.Property(e => e.FContact).HasColumnName("F联系人").HasMaxLength(50);
        builder.Property(e => e.FContactPhone).HasColumnName("F联系电话").HasMaxLength(20);
        builder.Property(e => e.FAgreedPrice).HasColumnName("F协议价格").HasMaxLength(200);
        builder.Property(e => e.FRemark).HasColumnName("F备注");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FEventId).HasDatabaseName("IX_CONF住宿酒店_活动ID");
    }
}
