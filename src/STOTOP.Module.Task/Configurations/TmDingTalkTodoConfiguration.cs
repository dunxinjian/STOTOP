using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmDingTalkTodoConfiguration : IEntityTypeConfiguration<TmDingTalkTodo>
{
    public void Configure(EntityTypeBuilder<TmDingTalkTodo> builder)
    {
        builder.ToTable("TM钉钉待办");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTaskId).HasColumnName("F任务ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FDingTalkTodoId).HasColumnName("F钉钉待办ID").HasMaxLength(100);
        builder.Property(e => e.FSyncStatus).HasColumnName("F同步状态");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FTaskId, e.FUserId })
            .IsUnique()
            .HasDatabaseName("UQ_TM钉钉待办_任务_用户");
    }
}
