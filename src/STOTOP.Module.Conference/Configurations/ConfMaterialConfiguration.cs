using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfMaterialConfiguration : IEntityTypeConfiguration<ConfMaterial>
{
    public void Configure(EntityTypeBuilder<ConfMaterial> builder)
    {
        builder.ToTable("CONF物品");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FEventId).HasColumnName("F活动ID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FCategory).HasColumnName("F类别").HasMaxLength(20);
        builder.Property(e => e.FSpecification).HasColumnName("F规格型号").HasMaxLength(200);
        builder.Property(e => e.FRequiredQuantity).HasColumnName("F需求数量");
        builder.Property(e => e.FReceivedQuantity).HasColumnName("F已到位数量").HasDefaultValue(0);
        builder.Property(e => e.FUnit).HasColumnName("F单位").HasMaxLength(20);
        builder.Property(e => e.FAcquisitionMethod).HasColumnName("F获取方式").HasMaxLength(20);
        builder.Property(e => e.FUnitPrice).HasColumnName("F单价").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FTotalPrice).HasColumnName("F总价").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FSupplier).HasColumnName("F供应商").HasMaxLength(200);
        builder.Property(e => e.FSupplierContact).HasColumnName("F供应商联系方式").HasMaxLength(50);
        builder.Property(e => e.FRequiredDate).HasColumnName("F需求日期").HasColumnType("date");
        builder.Property(e => e.FReceivedDate).HasColumnName("F到位日期").HasColumnType("date");
        builder.Property(e => e.FReturnDate).HasColumnName("F归还日期").HasColumnType("date");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(20).HasDefaultValue("计划中");
        builder.Property(e => e.FResponsible).HasColumnName("F负责人").HasMaxLength(50);
        builder.Property(e => e.FScheduleId).HasColumnName("F关联日程ID");
        builder.Property(e => e.FRemark).HasColumnName("F备注");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasOne(e => e.Schedule)
            .WithMany(e => e.Materials)
            .HasForeignKey(e => e.FScheduleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.FEventId).HasDatabaseName("IX_CONF物品_活动ID");
        builder.HasIndex(e => e.FCategory).HasDatabaseName("IX_CONF物品_类别");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_CONF物品_状态");
    }
}
