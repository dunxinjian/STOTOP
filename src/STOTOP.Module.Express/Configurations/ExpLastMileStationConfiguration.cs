using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpLastMileStationConfiguration : IEntityTypeConfiguration<ExpLastMileStation>
{
    public void Configure(EntityTypeBuilder<ExpLastMileStation> builder)
    {
        builder.ToTable("EXP末端驿站");

        builder.HasKey(e => e.FCode);
        builder.Property(e => e.FCode).HasColumnName("F编号").HasMaxLength(50);
        builder.Property(e => e.FStationType).HasColumnName("F类型").HasDefaultValue(1);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FAddress).HasColumnName("F地址").HasMaxLength(200);
        builder.Property(e => e.FBusinessHours).HasColumnName("F运营时间").HasMaxLength(100);
        builder.Property(e => e.FDailyVolume).HasColumnName("F日处理量");
        builder.Property(e => e.FShelfCount).HasColumnName("F货架数");
        builder.Property(e => e.FArea).HasColumnName("F面积").HasPrecision(10, 2);
        builder.Property(e => e.FContactName).HasColumnName("F联系人").HasMaxLength(50);
        builder.Property(e => e.FContactPhone).HasColumnName("F联系电话").HasMaxLength(20);
        builder.Property(e => e.FCooperationStartDate).HasColumnName("F合作开始日期");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FOrgId).IsUnique()
            .HasDatabaseName("IX_EXP末端驿站_F组织ID")
            .HasFilter("[F组织ID] IS NOT NULL");

        builder.HasOne(e => e.Organization)
            .WithMany()
            .HasForeignKey(e => e.FOrgId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
