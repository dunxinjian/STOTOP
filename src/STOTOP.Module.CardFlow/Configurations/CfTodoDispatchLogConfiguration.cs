using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfTodoDispatchLogConfiguration : IEntityTypeConfiguration<CfTodoDispatchLog>
{
    public void Configure(EntityTypeBuilder<CfTodoDispatchLog> builder)
    {
        builder.ToTable("CF待办分发日志");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.Property(e => e.FTodoItemId).HasColumnName("F待办项ID");
        builder.Property(e => e.FChannel).HasColumnName("F渠道").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FExternalTaskId).HasColumnName("F外部任务ID").HasMaxLength(200);
        builder.Property(e => e.FCorpId).HasColumnName("F企业CorpId").HasMaxLength(100);
        builder.Property(e => e.FDispatchStatus).HasColumnName("F分发状态").HasMaxLength(30).HasDefaultValue("dispatched");
        builder.Property(e => e.FLastCallbackEvent).HasColumnName("F最近回调事件").HasMaxLength(50);
        builder.Property(e => e.FLastCallbackAt).HasColumnName("F最近回调时间");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FTodoItemId, e.FChannel }).IsUnique().HasDatabaseName("UQ_CF待办分发日志_待办_渠道");
        builder.HasIndex(e => e.FExternalTaskId).HasDatabaseName("IX_CF待办分发日志_外部任务ID");
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_CF待办分发日志_租户ID");
    }
}
