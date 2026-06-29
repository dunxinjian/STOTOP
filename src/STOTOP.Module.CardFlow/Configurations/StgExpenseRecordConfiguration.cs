using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgExpenseRecordConfiguration : IEntityTypeConfiguration<StgExpenseRecord>
{
    public void Configure(EntityTypeBuilder<StgExpenseRecord> builder)
    {
        builder.ToTable("STG费用支出记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.F批次ID).HasColumnName("F批次ID");
        builder.Property(e => e.F处理状态).HasColumnName("F处理状态").HasDefaultValue(0);
        builder.Property(e => e.F错误信息).HasColumnName("F错误信息").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F关联凭证ID).HasColumnName("F关联凭证ID");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FDataScopeId).HasColumnName("FDataScopeId").HasMaxLength(64);
        builder.Property(e => e.FSourceWorkItemId).HasColumnName("FSourceWorkItemId");
        builder.Property(e => e.FIsRevoked).HasColumnName("FIsRevoked");
        builder.Property(e => e.FOrgId).HasColumnName("FOrgId");
        builder.Property(e => e.F账套ID).HasColumnName("F账套ID");
        builder.Property(e => e.F归属网点编号).HasColumnName("F归属网点编号").HasMaxLength(50);

        // 业务字段
        builder.Property(e => e.F数据ID).HasColumnName("F数据ID").HasMaxLength(100);
        builder.Property(e => e.F流程类型).HasColumnName("F流程类型").HasMaxLength(100);
        builder.Property(e => e.F费用类别).HasColumnName("F费用类别").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F费用摘要).HasColumnName("F费用摘要").HasMaxLength(500);
        builder.Property(e => e.F支出金额).HasColumnName("F支出金额").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F业务日期).HasColumnName("F业务日期").IsRequired();
        builder.Property(e => e.F收款方).HasColumnName("F收款方").HasMaxLength(200);
        builder.Property(e => e.F成本中心).HasColumnName("F成本中心").HasMaxLength(200);
        builder.Property(e => e.F审批编号).HasColumnName("F审批编号").HasMaxLength(100);
        builder.Property(e => e.F申请人).HasColumnName("F申请人").HasMaxLength(100);
        builder.Property(e => e.F申请人部门).HasColumnName("F申请人部门").HasMaxLength(200);
        builder.Property(e => e.F审批结果).HasColumnName("F审批结果").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F完成时间).HasColumnName("F完成时间");

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG费用支出记录_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG费用支出记录_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");
    }
}
