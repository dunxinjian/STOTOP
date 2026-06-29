using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.KSF.Entities;

namespace STOTOP.Module.KSF.Configurations;

public class KsfPlanConfiguration : IEntityTypeConfiguration<KsfPlan>
{
    public void Configure(EntityTypeBuilder<KsfPlan> builder)
    {
        builder.ToTable("KSF岗位方案");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F名称).HasColumnName("F名称").HasMaxLength(128).IsRequired();
        builder.Property(e => e.F岗位ID).HasColumnName("F岗位ID");
        builder.Property(e => e.F生效起期).HasColumnName("F生效起期");
        builder.Property(e => e.F生效止期).HasColumnName("F生效止期");
        builder.Property(e => e.F启用状态).HasColumnName("F启用状态").HasDefaultValue(true);
        builder.Property(e => e.F运行模式).HasColumnName("F运行模式").HasDefaultValue(0);
        builder.Property(e => e.F门槛规则JSON).HasColumnName("F门槛规则JSON");
        builder.Property(e => e.F岗位月加薪基数).HasColumnName("F岗位月加薪基数").HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(e => e.F负责人ID).HasColumnName("F负责人ID");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.F更新时间).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F岗位ID }).HasDatabaseName("IX_KSF岗位方案_组织_岗位");
    }
}
