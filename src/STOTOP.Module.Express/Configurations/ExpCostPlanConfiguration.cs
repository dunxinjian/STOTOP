using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpCostPlanConfiguration : IEntityTypeConfiguration<ExpCostPlan>
{
    public void Configure(EntityTypeBuilder<ExpCostPlan> builder)
    {
        builder.ToTable("EXP成本方案");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FPlanName).HasColumnName("F方案名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasMany(e => e.Items)
            .WithOne(i => i.Plan)
            .HasForeignKey(i => i.FPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Exclusions)
            .WithOne(ex => ex.Plan)
            .HasForeignKey(ex => ex.FPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.FOrgId, e.FBrandCode, e.FStatus })
            .HasDatabaseName("IX_EXP成本方案_组织品牌状态");
    }
}
