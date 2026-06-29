using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongPhotoQcConfiguration : IEntityTypeConfiguration<StgShentongPhotoQc>
{
    public void Configure(EntityTypeBuilder<StgShentongPhotoQc> builder)
    {
        builder.ToTable("STG申通_照片质检明细");

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

        // 业务字段（25 列，全部可空字符串）
        builder.Property(e => e.F单号).HasColumnName("F单号").HasMaxLength(200);
        builder.Property(e => e.F业务类型).HasColumnName("F业务类型").HasMaxLength(200);
        builder.Property(e => e.F是否履约).HasColumnName("F是否履约").HasMaxLength(200);
        builder.Property(e => e.F签收人).HasColumnName("F签收人").HasMaxLength(200);
        builder.Property(e => e.F是否质检合格).HasColumnName("F是否质检合格").HasMaxLength(200);
        builder.Property(e => e.F不合格类型).HasColumnName("F不合格类型").HasMaxLength(200);
        builder.Property(e => e.F是否上门).HasColumnName("F是否上门").HasMaxLength(200);
        builder.Property(e => e.F小件员名称).HasColumnName("F小件员名称").HasMaxLength(200);
        builder.Property(e => e.F小件员编码).HasColumnName("F小件员编码").HasMaxLength(200);
        builder.Property(e => e.F网点编码).HasColumnName("F网点编码").HasMaxLength(200);
        builder.Property(e => e.F网点名称).HasColumnName("F网点名称").HasMaxLength(200);
        builder.Property(e => e.F大区编码).HasColumnName("F大区编码").HasMaxLength(200);
        builder.Property(e => e.F大区名称).HasColumnName("F大区名称").HasMaxLength(200);
        builder.Property(e => e.F省区编码).HasColumnName("F省区编码").HasMaxLength(200);
        builder.Property(e => e.F省区名称).HasColumnName("F省区名称").HasMaxLength(200);
        builder.Property(e => e.F片区编码).HasColumnName("F片区编码").HasMaxLength(200);
        builder.Property(e => e.F片区名称).HasColumnName("F片区名称").HasMaxLength(200);
        builder.Property(e => e.F收件地址).HasColumnName("F收件地址").HasMaxLength(200);
        builder.Property(e => e.F收件手机号).HasColumnName("F收件手机号").HasMaxLength(200);
        builder.Property(e => e.F投诉类型).HasColumnName("F投诉类型").HasMaxLength(200);
        builder.Property(e => e.F投诉内容).HasColumnName("F投诉内容").HasMaxLength(200);
        builder.Property(e => e.F投诉时间).HasColumnName("F投诉时间").HasMaxLength(200);
        builder.Property(e => e.F投诉来源).HasColumnName("F投诉来源").HasMaxLength(200);
        builder.Property(e => e.F是否拍照).HasColumnName("F是否拍照").HasMaxLength(200);
        builder.Property(e => e.F分区).HasColumnName("F分区").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_照片质检明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_照片质检明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 照片质检明细每单号一行，按单号去重；1 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F单号, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F单号] IS NOT NULL AND [F单号] != ''")
            .HasDatabaseName("UX_STG申通_照片质检明细_单号_未撤销");
    }
}
