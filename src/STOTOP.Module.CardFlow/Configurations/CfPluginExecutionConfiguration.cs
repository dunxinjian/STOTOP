using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfPluginExecutionConfiguration : IEntityTypeConfiguration<CfPluginExecution>
{
    public void Configure(EntityTypeBuilder<CfPluginExecution> builder)
    {
        builder.ToTable("CF自动插件_执行记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBatchId).HasColumnName("F批次ID");
        builder.Property(e => e.FOrgId).HasColumnName("FOrgId");
        builder.Property(e => e.FAutoPluginName).HasColumnName("FAutoPlugin名称").HasMaxLength(100);
        builder.Property(e => e.FAutoPluginIndex).HasColumnName("FAutoPlugin索引");
        builder.Property(e => e.FStatus).HasColumnName("F状态");
        builder.Property(e => e.FStartTime).HasColumnName("F开始时间");
        builder.Property(e => e.FEndTime).HasColumnName("F结束时间");
        builder.Property(e => e.FErrorMessage).HasColumnName("F错误信息").HasMaxLength(2000);
        builder.Property(e => e.FSummaryJson).HasColumnName("F摘要JSON");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => e.FBatchId).HasDatabaseName("IX_CF自动插件_执行记录_批次ID");
    }
}
