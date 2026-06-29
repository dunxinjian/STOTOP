using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Dormitory.Entities;

namespace STOTOP.Module.Dormitory.Configurations;

public class DorResidenceConfiguration : IEntityTypeConfiguration<DorResidence>
{
    public void Configure(EntityTypeBuilder<DorResidence> builder)
    {
        builder.ToTable("DOR入住记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FBedId).HasColumnName("F床位ID").IsRequired();
        builder.Property(e => e.FEmployeeId).HasColumnName("F员工ID").IsRequired();
        builder.Property(e => e.FCheckInDate).HasColumnName("F入住日期").IsRequired();
        builder.Property(e => e.FCheckOutDate).HasColumnName("F退宿日期");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FEmployeeId).HasDatabaseName("IX_DOR入住记录_员工ID");
        builder.HasIndex(e => e.FBedId).HasDatabaseName("IX_DOR入住记录_床位ID");

        builder.HasOne(e => e.Bed)
            .WithMany()
            .HasForeignKey(e => e.FBedId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
