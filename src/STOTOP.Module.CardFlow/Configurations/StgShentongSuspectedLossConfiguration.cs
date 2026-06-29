using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongSuspectedLossConfiguration : IEntityTypeConfiguration<StgShentongSuspectedLoss>
{
    public void Configure(EntityTypeBuilder<StgShentongSuspectedLoss> builder)
    {
        builder.ToTable("STG申通_疑似遗失明细");

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

        // 业务字段（51 列，全部可空字符串）
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F先行理赔状态).HasColumnName("F先行理赔状态").HasMaxLength(200);
        builder.Property(e => e.F是否找回).HasColumnName("F是否找回").HasMaxLength(200);
        builder.Property(e => e.F签收标识).HasColumnName("F签收标识").HasMaxLength(200);
        builder.Property(e => e.F实际金额).HasColumnName("F实际金额").HasMaxLength(200);
        builder.Property(e => e.F是否疫情件).HasColumnName("F是否疫情件").HasMaxLength(200);
        builder.Property(e => e.F内件品名).HasColumnName("F内件品名").HasMaxLength(200);
        builder.Property(e => e.F结算重量kg).HasColumnName("F结算重量kg").HasMaxLength(200);
        builder.Property(e => e.F订单来源).HasColumnName("F订单来源").HasMaxLength(200);
        builder.Property(e => e.F3日轨迹中断触发类型).HasColumnName("F3日轨迹中断触发类型").HasMaxLength(200);
        builder.Property(e => e.F包号).HasColumnName("F包号").HasMaxLength(200);
        builder.Property(e => e.F集包站点).HasColumnName("F集包站点").HasMaxLength(200);
        builder.Property(e => e.F扫描站点).HasColumnName("F扫描站点").HasMaxLength(200);
        builder.Property(e => e.F扫描站点所属省份).HasColumnName("F扫描站点所属省份").HasMaxLength(200);
        builder.Property(e => e.F扫描站点所属南北区).HasColumnName("F扫描站点所属南北区").HasMaxLength(200);
        builder.Property(e => e.F最后扫描时间).HasColumnName("F最后扫描时间").HasMaxLength(200);
        builder.Property(e => e.F扫描操作人).HasColumnName("F扫描操作人").HasMaxLength(200);
        builder.Property(e => e.F业务员).HasColumnName("F业务员").HasMaxLength(200);
        builder.Property(e => e.F下一节点操作截止时间).HasColumnName("F下一节点操作截止时间").HasMaxLength(200);
        builder.Property(e => e.F3日轨迹中断触发时间).HasColumnName("F3日轨迹中断触发时间").HasMaxLength(200);
        builder.Property(e => e.F找货责任方1).HasColumnName("F找货责任方1").HasMaxLength(200);
        builder.Property(e => e.F找件责任1所属网点名称).HasColumnName("F找件责任1所属网点名称").HasMaxLength(200);
        builder.Property(e => e.F找货责任方2).HasColumnName("F找货责任方2").HasMaxLength(200);
        builder.Property(e => e.F找件责任2所属网点名称).HasColumnName("F找件责任2所属网点名称").HasMaxLength(200);
        builder.Property(e => e.F运输任务号).HasColumnName("F运输任务号").HasMaxLength(200);
        builder.Property(e => e.F承运商).HasColumnName("F承运商").HasMaxLength(200);
        builder.Property(e => e.F车牌号).HasColumnName("F车牌号").HasMaxLength(200);
        builder.Property(e => e.F揽收省份).HasColumnName("F揽收省份").HasMaxLength(200);
        builder.Property(e => e.F揽收网点).HasColumnName("F揽收网点").HasMaxLength(200);
        builder.Property(e => e.F问题件类型).HasColumnName("F问题件类型").HasMaxLength(200);
        builder.Property(e => e.F退回件标识).HasColumnName("F退回件标识").HasMaxLength(200);
        builder.Property(e => e.F拦截件标识).HasColumnName("F拦截件标识").HasMaxLength(200);
        builder.Property(e => e.F停滞用时).HasColumnName("F停滞用时").HasMaxLength(200);
        builder.Property(e => e.F是否理赔).HasColumnName("F是否理赔").HasMaxLength(200);
        builder.Property(e => e.F目的地省份).HasColumnName("F目的地省份").HasMaxLength(200);
        builder.Property(e => e.F目的地网点).HasColumnName("F目的地网点").HasMaxLength(200);
        builder.Property(e => e.F找回时的扫描类型).HasColumnName("F找回时的扫描类型").HasMaxLength(200);
        builder.Property(e => e.F找回时的扫描站点).HasColumnName("F找回时的扫描站点").HasMaxLength(200);
        builder.Property(e => e.F找回时间).HasColumnName("F找回时间").HasMaxLength(200);
        builder.Property(e => e.F找回时长h).HasColumnName("F找回时长h").HasMaxLength(200);
        builder.Property(e => e.F下一站).HasColumnName("F下一站").HasMaxLength(200);
        builder.Property(e => e.F下一站省份).HasColumnName("F下一站省份").HasMaxLength(200);
        builder.Property(e => e.F下一站所属南北区).HasColumnName("F下一站所属南北区").HasMaxLength(200);
        builder.Property(e => e.F责任方1所属省区).HasColumnName("F责任方1所属省区").HasMaxLength(200);
        builder.Property(e => e.F责任方2所属省区).HasColumnName("F责任方2所属省区").HasMaxLength(200);
        builder.Property(e => e.F订单网点).HasColumnName("F订单网点").HasMaxLength(200);
        builder.Property(e => e.F订单省份).HasColumnName("F订单省份").HasMaxLength(200);
        builder.Property(e => e.F考核剔除项).HasColumnName("F考核剔除项").HasMaxLength(200);
        builder.Property(e => e.F商家编码).HasColumnName("F商家编码").HasMaxLength(200);
        builder.Property(e => e.F商家名称).HasColumnName("F商家名称").HasMaxLength(200);
        builder.Property(e => e.F任务不发起原因).HasColumnName("F任务不发起原因").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_疑似遗失明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_疑似遗失明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 疑似遗失明细每运单一行，按运单号去重；1 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F运单号, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != ''")
            .HasDatabaseName("UX_STG申通_疑似遗失明细_运单号_未撤销");
    }
}
