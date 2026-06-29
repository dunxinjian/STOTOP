using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmKeyResultConfiguration : IEntityTypeConfiguration<TmKeyResult>
{
    public void Configure(EntityTypeBuilder<TmKeyResult> builder)
    {
        builder.ToTable("TM关键成果");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(32).IsRequired();
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FGoalId).HasColumnName("F目标ID");
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FMeasureType).HasColumnName("F度量方式");
        builder.Property(e => e.FTargetValue).HasColumnName("F目标值").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FCurrentValue).HasColumnName("F当前值").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.FStartValue).HasColumnName("F起始值").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.FUnit).HasColumnName("F单位").HasMaxLength(20);
        builder.Property(e => e.FWeight).HasColumnName("F权重").HasDefaultValue(100);
        builder.Property(e => e.FProgress).HasColumnName("F进度").HasDefaultValue(0);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FResponsibleId).HasColumnName("F责任人ID");
        builder.Property(e => e.FSort).HasColumnName("F排序").HasDefaultValue(0);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => e.FGoalId).HasDatabaseName("IX_TM关键成果_目标ID");
        builder.HasIndex(e => e.FResponsibleId).HasDatabaseName("IX_TM关键成果_责任人ID");
    }
}
