using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmProjectConfiguration : IEntityTypeConfiguration<TmProject>
{
    public void Configure(EntityTypeBuilder<TmProject> builder)
    {
        builder.ToTable("TM项目");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(32).IsRequired();
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FDescription).HasColumnName("F描述");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FGoalId).HasColumnName("F目标ID");
        builder.Property(e => e.FManagerId).HasColumnName("F负责人ID");
        builder.Property(e => e.FStartDate).HasColumnName("F开始日期");
        builder.Property(e => e.FEndDate).HasColumnName("F截止日期");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_TM项目_组织ID");
        builder.HasIndex(e => e.FGoalId).HasDatabaseName("IX_TM项目_目标ID");
        builder.HasIndex(e => e.FManagerId).HasDatabaseName("IX_TM项目_负责人ID");

        builder.HasMany(e => e.Members)
            .WithOne(e => e.Project)
            .HasForeignKey(e => e.FProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Tasks)
            .WithOne(e => e.Project)
            .HasForeignKey(e => e.FProjectId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
