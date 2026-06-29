using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfActionLogConfiguration : IEntityTypeConfiguration<CfActionLog>
{
    public void Configure(EntityTypeBuilder<CfActionLog> builder)
    {
        builder.ToTable("CF操作日志");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCardId).HasColumnName("F卡片ID");
        builder.Property(e => e.FStageInstanceId).HasColumnName("F节点实例ID");
        builder.Property(e => e.FActionType).HasColumnName("F操作类型").HasMaxLength(30);
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FOperatorName).HasColumnName("F操作人姓名").HasMaxLength(100);
        builder.Property(e => e.FOperationTime).HasColumnName("F操作时间");
        builder.Property(e => e.FOpinion).HasColumnName("F意见").HasMaxLength(500);
        builder.Property(e => e.FDetailJson).HasColumnName("F详情JSON");

        builder.HasIndex(e => e.FCardId).HasDatabaseName("IX_CF操作日志_卡片");
    }
}
