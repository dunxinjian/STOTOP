using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Dormitory.Entities;

namespace STOTOP.Module.Dormitory.Configurations;

public class DorFacilityConfiguration : IEntityTypeConfiguration<DorFacility>
{
    public void Configure(EntityTypeBuilder<DorFacility> builder)
    {
        builder.ToTable("DOR设施登记");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FRoomId).HasColumnName("F房间ID").IsRequired();
        builder.Property(e => e.FFacilityName).HasColumnName("F设施名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FQuantity).HasColumnName("F数量").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FRoomId).HasDatabaseName("IX_DOR设施登记_房间ID");

        builder.HasOne(e => e.Room)
            .WithMany()
            .HasForeignKey(e => e.FRoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
