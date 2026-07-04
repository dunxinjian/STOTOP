using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class PltPlanConfiguration : IEntityTypeConfiguration<PltPlan>
{
    public void Configure(EntityTypeBuilder<PltPlan> builder)
    {
        builder.ToTable("PLT套餐");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FCode).HasColumnName("F编号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FMaxUsers).HasColumnName("F最大用户数").HasDefaultValue(0);
        builder.Property(e => e.FMaxOutlets).HasColumnName("F最大网点数").HasDefaultValue(0);
        builder.Property(e => e.FModuleFlags).HasColumnName("F模块开关");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FCode).IsUnique().HasDatabaseName("UQ_PLT套餐_编号");
    }
}
