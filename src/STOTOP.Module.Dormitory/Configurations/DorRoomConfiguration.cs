using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Dormitory.Entities;

namespace STOTOP.Module.Dormitory.Configurations;

public class DorRoomConfiguration : IEntityTypeConfiguration<DorRoom>
{
    public void Configure(EntityTypeBuilder<DorRoom> builder)
    {
        builder.ToTable("DOR房间");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FBuildingId).HasColumnName("F楼栋ID");
        builder.Property(e => e.FFloor).HasColumnName("F楼层");
        builder.Property(e => e.FRoomNumber).HasColumnName("F房号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FBedsCount).HasColumnName("F床位数").HasDefaultValue(4);
        builder.Property(e => e.FRoomType).HasColumnName("F房间类型").HasMaxLength(50);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FBuildingId, e.FRoomNumber }).IsUnique().HasDatabaseName("IX_DOR房间_楼栋房号");
        builder.HasIndex(e => e.FBuildingId).HasDatabaseName("IX_DOR房间_楼栋ID");

        builder.HasMany(e => e.Beds)
            .WithOne(e => e.Room)
            .HasForeignKey(e => e.FRoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
