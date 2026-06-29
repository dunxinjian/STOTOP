using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongPenetrationConfiguration : IEntityTypeConfiguration<StgShentongPenetration>
{
    public void Configure(EntityTypeBuilder<StgShentongPenetration> builder)
    {
        builder.ToTable("STG申通_渗透建站考核");

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

        // 业务字段（24 列，全部可空字符串）
        builder.Property(e => e.F统计周期).HasColumnName("F统计周期").HasMaxLength(200);
        builder.Property(e => e.F网点名称).HasColumnName("F网点名称").HasMaxLength(200);
        builder.Property(e => e.F网点编号).HasColumnName("F网点编号").HasMaxLength(200);
        builder.Property(e => e.F自建渗透率当月目标).HasColumnName("F自建渗透率当月目标").HasMaxLength(200);
        builder.Property(e => e.F已认证自建渗透率).HasColumnName("F已认证自建渗透率").HasMaxLength(200);
        builder.Property(e => e.F已认证自建渗透率差值).HasColumnName("F已认证自建渗透率差值").HasMaxLength(200);
        builder.Property(e => e.F已认证自建渗透率环比).HasColumnName("F已认证自建渗透率环比").HasMaxLength(200);
        builder.Property(e => e.F总入库量).HasColumnName("F总入库量").HasMaxLength(200);
        builder.Property(e => e.F已认证自建入库量).HasColumnName("F已认证自建入库量").HasMaxLength(200);
        builder.Property(e => e.F建站当季目标).HasColumnName("F建站当季目标").HasMaxLength(200);
        builder.Property(e => e.F建站当月目标).HasColumnName("F建站当月目标").HasMaxLength(200);
        builder.Property(e => e.F菜鸟活跃).HasColumnName("F菜鸟活跃").HasMaxLength(200);
        builder.Property(e => e.F喵站活跃).HasColumnName("F喵站活跃").HasMaxLength(200);
        builder.Property(e => e.F多多活跃).HasColumnName("F多多活跃").HasMaxLength(200);
        builder.Property(e => e.F喵柜抵扣建站数).HasColumnName("F喵柜抵扣建站数").HasMaxLength(200);
        builder.Property(e => e.F建站待完成).HasColumnName("F建站待完成").HasMaxLength(200);
        builder.Property(e => e.F菜鸟当月新增).HasColumnName("F菜鸟当月新增").HasMaxLength(200);
        builder.Property(e => e.F建柜目标).HasColumnName("F建柜目标").HasMaxLength(200);
        builder.Property(e => e.F喵柜激活格口数).HasColumnName("F喵柜激活格口数").HasMaxLength(200);
        builder.Property(e => e.F喵柜激活格口数环比).HasColumnName("F喵柜激活格口数环比").HasMaxLength(200);
        builder.Property(e => e.F喵柜待完成格口数).HasColumnName("F喵柜待完成格口数").HasMaxLength(200);
        builder.Property(e => e.F全cp日均入库量).HasColumnName("F全cp日均入库量").HasMaxLength(200);
        builder.Property(e => e.F申通日均入库量).HasColumnName("F申通日均入库量").HasMaxLength(200);
        builder.Property(e => e.F喵柜组数).HasColumnName("F喵柜组数").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_渗透建站考核_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_渗透建站考核_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 渗透建站考核 1 行/网点/周期，按「网点编号 + 统计周期」去重；2 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F网点编号, e.F统计周期, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F网点编号] IS NOT NULL AND [F网点编号] != ''")
            .HasDatabaseName("UX_STG申通_渗透建站考核_网点周期_未撤销");
    }
}
