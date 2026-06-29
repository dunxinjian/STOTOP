using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfTableConfiguration : IEntityTypeConfiguration<ConfTable>
{
    public void Configure(EntityTypeBuilder<ConfTable> builder)
    {
        builder.ToTable("CONF桌次");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FMealPlanId).HasColumnName("F餐食计划ID");
        builder.Property(e => e.FTableNumber).HasColumnName("F桌号");
        builder.Property(e => e.FTableName).HasColumnName("F桌名").HasMaxLength(50);
        builder.Property(e => e.FSeatCount).HasColumnName("F座位数");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(200);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasOne(e => e.MealPlan)
            .WithMany(e => e.Tables)
            .HasForeignKey(e => e.FMealPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.FMealPlanId).HasDatabaseName("IX_CONF桌次_餐食计划ID");
    }
}
