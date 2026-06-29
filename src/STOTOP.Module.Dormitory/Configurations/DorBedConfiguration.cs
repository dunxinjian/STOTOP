using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Dormitory.Entities;

namespace STOTOP.Module.Dormitory.Configurations;

public class DorBedConfiguration : IEntityTypeConfiguration<DorBed>
{
    public void Configure(EntityTypeBuilder<DorBed> builder)
    {
        builder.ToTable("DOR床位");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FRoomId).HasColumnName("F房间ID");
        builder.Property(e => e.FBedNumber).HasColumnName("F床位号").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FBedType).HasColumnName("F床位类型").HasMaxLength(20).HasDefaultValue("lower").IsRequired();
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FRoomId, e.FBedNumber }).IsUnique().HasDatabaseName("IX_DOR床位_房间床位号");
        builder.HasIndex(e => e.FRoomId).HasDatabaseName("IX_DOR床位_房间ID");
    }
}
