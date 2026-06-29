using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmPerformancePeriodConfiguration : IEntityTypeConfiguration<TmPerformancePeriod>
{
    public void Configure(EntityTypeBuilder<TmPerformancePeriod> builder)
    {
        builder.ToTable("TM绩效考核周期");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(32).IsRequired();
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FType).HasColumnName("F类型");
        builder.Property(e => e.FStartDate).HasColumnName("F开始日期");
        builder.Property(e => e.FEndDate).HasColumnName("F截止日期");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_TM绩效考核周期_组织ID");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_TM绩效考核周期_状态");

        builder.HasMany(e => e.Records)
            .WithOne(e => e.Period)
            .HasForeignKey(e => e.FPeriodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
