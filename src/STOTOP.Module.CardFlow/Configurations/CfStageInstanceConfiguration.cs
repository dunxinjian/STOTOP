using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfStageInstanceConfiguration : IEntityTypeConfiguration<CfStageInstance>
{
    public void Configure(EntityTypeBuilder<CfStageInstance> builder)
    {
        builder.ToTable("CF节点执行实例");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCardId).HasColumnName("F卡片ID");
        builder.Property(e => e.FStageDefinitionId).HasColumnName("F节点定义ID");
        builder.Property(e => e.FStageName).HasColumnName("F节点名称").HasMaxLength(100);
        builder.Property(e => e.FType).HasColumnName("F类型").HasMaxLength(30);
        builder.Property(e => e.FApprovalMode).HasColumnName("F审批模式").HasMaxLength(30);
        builder.Property(e => e.FRound).HasColumnName("F轮次");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(30);
        builder.Property(e => e.FStartTime).HasColumnName("F开始时间");
        builder.Property(e => e.FCompletedTime).HasColumnName("F完成时间");
        builder.Property(e => e.FActivatedTime).HasColumnName("F激活时间");
        builder.Property(e => e.FFinalAction).HasColumnName("F最终操作").HasMaxLength(30);
        builder.Property(e => e.FOpinion).HasColumnName("F意见").HasMaxLength(500);
        builder.Property(e => e.FSupplementDataJson).HasColumnName("F补充数据JSON");
        builder.Property(e => e.FIsDynamicInsert).HasColumnName("F是否动态插入");
        builder.Property(e => e.FInsertSourceStageId).HasColumnName("F插入来源节点ID");
        builder.Property(e => e.FInsertContextJson).HasColumnName("F插入上下文JSON");
        builder.Property(e => e.FIsTimeout).HasColumnName("F是否超时");
        builder.Property(e => e.FRowVersion).HasColumnName("F乐观锁").IsRowVersion();

        builder.HasIndex(e => e.FCardId).HasDatabaseName("IX_CF节点执行实例_卡片");
        builder.HasIndex(e => new { e.FCardId, e.FRound }).HasDatabaseName("IX_CF节点执行实例_卡片轮次");
    }
}
