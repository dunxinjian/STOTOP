using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmGoalConfiguration : IEntityTypeConfiguration<TmGoal>
{
    public void Configure(EntityTypeBuilder<TmGoal> builder)
    {
        builder.ToTable("TM目标");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(32).IsRequired();
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FDescription).HasColumnName("F描述");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FGoalOrgId).HasColumnName("F目标组织ID");
        builder.Property(e => e.FResponsibleId).HasColumnName("F责任人ID");
        builder.Property(e => e.FParentId).HasColumnName("F父ID").HasDefaultValue(0L);
        builder.Property(e => e.FLevel).HasColumnName("F层级").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FStartDate).HasColumnName("F开始日期").HasColumnType("date");
        builder.Property(e => e.FEndDate).HasColumnName("F截止日期").HasColumnType("date");
        builder.Property(e => e.FProgress).HasColumnName("F进度").HasDefaultValue(0);
        builder.Property(e => e.FWeight).HasColumnName("F权重").HasDefaultValue(100);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => e.FParentId).HasDatabaseName("IX_TM目标_父级ID");
        builder.HasIndex(e => e.FGoalOrgId).HasDatabaseName("IX_TM目标_目标组织ID");
        builder.HasIndex(e => e.FResponsibleId).HasDatabaseName("IX_TM目标_责任人ID");

        builder.HasMany(e => e.KeyResults)
            .WithOne(e => e.Goal)
            .HasForeignKey(e => e.FGoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Children)
            .WithOne()
            .HasForeignKey(e => e.FParentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
