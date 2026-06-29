using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Dormitory.Entities;

namespace STOTOP.Module.Dormitory.Configurations;

public class DorBuildingConfiguration : IEntityTypeConfiguration<DorBuilding>
{
    public void Configure(EntityTypeBuilder<DorBuilding> builder)
    {
        builder.ToTable("DOR宿舍楼栋");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FAddress).HasColumnName("F地址").HasMaxLength(500);
        builder.Property(e => e.FTotalFloors).HasColumnName("F总楼层").HasDefaultValue(1);
        builder.Property(e => e.FManagerId).HasColumnName("F管理员ID");
        builder.Property(e => e.FDormitoryType).HasColumnName("F宿舍类型").HasMaxLength(50);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FCode).IsUnique().HasDatabaseName("IX_DOR宿舍楼栋_编码");
        builder.HasIndex(e => e.FName).HasDatabaseName("IX_DOR宿舍楼栋_名称");

        builder.HasMany(e => e.Rooms)
            .WithOne(e => e.Building)
            .HasForeignKey(e => e.FBuildingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
