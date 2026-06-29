using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongCourierFulfillConfiguration : IEntityTypeConfiguration<StgShentongCourierFulfill>
{
    public void Configure(EntityTypeBuilder<StgShentongCourierFulfill> builder)
    {
        builder.ToTable("STG申通_小件员履约指标");

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

        // 业务字段（19 列，均为 row2 全局唯一名）
        builder.Property(e => e.F所属网点).HasColumnName("F所属网点").HasMaxLength(200);
        builder.Property(e => e.F所属小件员).HasColumnName("F所属小件员").HasMaxLength(200);
        builder.Property(e => e.F当日派签量).HasColumnName("F当日派签量").HasMaxLength(200);
        builder.Property(e => e.F未当日派签量).HasColumnName("F未当日派签量").HasMaxLength(200);
        builder.Property(e => e.F当日派签率).HasColumnName("F当日派签率").HasMaxLength(200);
        builder.Property(e => e.F应上门量).HasColumnName("F应上门量").HasMaxLength(200);
        builder.Property(e => e.F未上门量).HasColumnName("F未上门量").HasMaxLength(200);
        builder.Property(e => e.F按需上门率).HasColumnName("F按需上门率").HasMaxLength(200);
        builder.Property(e => e.F客诉发起量).HasColumnName("F客诉发起量").HasMaxLength(200);
        builder.Property(e => e.F工单定责量).HasColumnName("F工单定责量").HasMaxLength(200);
        builder.Property(e => e.F客诉发起率).HasColumnName("F客诉发起率").HasMaxLength(200);
        builder.Property(e => e.F虚假电联).HasColumnName("F虚假电联").HasMaxLength(200);
        builder.Property(e => e.F无效电联).HasColumnName("F无效电联").HasMaxLength(200);
        builder.Property(e => e.F双签).HasColumnName("F双签").HasMaxLength(200);
        builder.Property(e => e.F照片定位虚假).HasColumnName("F照片定位虚假").HasMaxLength(200);
        builder.Property(e => e.F签收文本不规范).HasColumnName("F签收文本不规范").HasMaxLength(200);
        builder.Property(e => e.F引导代收).HasColumnName("F引导代收").HasMaxLength(200);
        builder.Property(e => e.F回访虚假量).HasColumnName("F回访虚假量").HasMaxLength(200);
        builder.Property(e => e.F回访真实率).HasColumnName("F回访真实率").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_小件员履约指标_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_小件员履约指标_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 员工级 1 行/网点/小件员（此文件无日期列），按「所属网点 + 所属小件员」去重。
        builder.HasIndex(e => new { e.F所属网点, e.F所属小件员, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F所属网点] IS NOT NULL AND [F所属网点] != ''")
            .HasDatabaseName("UX_STG申通_小件员履约指标_网点小件员_未撤销");
    }
}
