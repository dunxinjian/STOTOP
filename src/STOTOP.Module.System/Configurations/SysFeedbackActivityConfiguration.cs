using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysFeedbackActivityConfiguration : IEntityTypeConfiguration<SysFeedbackActivity>
{
    public void Configure(EntityTypeBuilder<SysFeedbackActivity> builder)
    {
        builder.ToTable("SYS反馈动态");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FFeedbackId).HasColumnName("F反馈ID");
        builder.Property(e => e.FActorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FAction).HasColumnName("F动作").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FContent).HasColumnName("F内容").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FFromStatus).HasColumnName("F原状态");
        builder.Property(e => e.FToStatus).HasColumnName("F目标状态");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FFeedbackId).HasDatabaseName("IX_SYS反馈动态_反馈ID");
        builder.HasIndex(e => new { e.FOrgId, e.FCreateTime }).HasDatabaseName("IX_SYS反馈动态_组织_时间");
    }
}
