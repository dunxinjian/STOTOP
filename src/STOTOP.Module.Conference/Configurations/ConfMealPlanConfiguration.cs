using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfMealPlanConfiguration : IEntityTypeConfiguration<ConfMealPlan>
{
    public void Configure(EntityTypeBuilder<ConfMealPlan> builder)
    {
        builder.ToTable("CONF餐食计划");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FEventId).HasColumnName("F活动ID");
        builder.Property(e => e.FDate).HasColumnName("F日期").HasColumnType("date");
        builder.Property(e => e.FMealType).HasColumnName("F餐次").HasMaxLength(10).IsRequired();
        builder.Property(e => e.FDiningMode).HasColumnName("F用餐方式").HasMaxLength(20);
        builder.Property(e => e.FLocation).HasColumnName("F地点").HasMaxLength(200);
        builder.Property(e => e.FExpectedCount).HasColumnName("F预计人数");
        builder.Property(e => e.FActualCount).HasColumnName("F实际人数");
        builder.Property(e => e.FRemark).HasColumnName("F备注");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FEventId, e.FDate }).HasDatabaseName("IX_CONF餐食计划_活动日期");
    }
}
