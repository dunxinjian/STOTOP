using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysFeedbackCardConfiguration : IEntityTypeConfiguration<SysFeedbackCard>
{
    public void Configure(EntityTypeBuilder<SysFeedbackCard> builder)
    {
        builder.ToTable("SYS反馈卡片");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(64).IsRequired();
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FType).HasColumnName("F类型");
        builder.Property(e => e.FModule).HasColumnName("F所属模块").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FSeverity).HasColumnName("F严重程度").HasDefaultValue(2);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FSubmitterId).HasColumnName("F提交人ID");
        builder.Property(e => e.FAssigneeId).HasColumnName("F负责人ID");
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FReproduceSteps).HasColumnName("F复现步骤").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FExpectedResult).HasColumnName("F期望结果").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FActualResult).HasColumnName("F实际结果").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FAttachmentLinks).HasColumnName("F附件链接").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FPageUrl).HasColumnName("F页面地址").HasMaxLength(500);
        builder.Property(e => e.FClientInfo).HasColumnName("F客户端信息").HasMaxLength(1000);
        builder.Property(e => e.FVersion).HasColumnName("F关联版本").HasMaxLength(100);
        builder.Property(e => e.FConclusion).HasColumnName("F处理结论").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FClosedTime).HasColumnName("F关闭时间");

        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => new { e.FOrgId, e.FStatus }).HasDatabaseName("IX_SYS反馈卡片_组织_状态");
        builder.HasIndex(e => new { e.FOrgId, e.FSeverity }).HasDatabaseName("IX_SYS反馈卡片_组织_严重程度");
        builder.HasIndex(e => e.FSubmitterId).HasDatabaseName("IX_SYS反馈卡片_提交人");
        builder.HasIndex(e => e.FAssigneeId).HasDatabaseName("IX_SYS反馈卡片_负责人");
    }
}
