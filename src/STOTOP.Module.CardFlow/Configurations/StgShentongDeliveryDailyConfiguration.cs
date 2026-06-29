using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongDeliveryDailyConfiguration : IEntityTypeConfiguration<StgShentongDeliveryDaily>
{
    public void Configure(EntityTypeBuilder<StgShentongDeliveryDaily> builder)
    {
        builder.ToTable("STG申通派件日明细");

        // ===== 系统列 =====
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.F批次ID).HasColumnName("F批次ID");
        builder.Property(e => e.F原始行号).HasColumnName("F原始行号");
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
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);  // 不加 IsRequired

        // ===== 业务列 =====
        builder.Property(e => e.F结算日期).HasColumnName("F结算日期").HasColumnType("date");
        builder.Property(e => e.F网点编号).HasColumnName("F网点编号").HasMaxLength(50);
        builder.Property(e => e.F网点名称).HasColumnName("F网点名称").HasMaxLength(200);
        builder.Property(e => e.F承包区编号).HasColumnName("F承包区编号").HasMaxLength(50);
        builder.Property(e => e.F承包区名称).HasColumnName("F承包区名称").HasMaxLength(200);
        builder.Property(e => e.F业务员编码).HasColumnName("F业务员编码").HasMaxLength(50);
        builder.Property(e => e.F业务员名称).HasColumnName("F业务员名称").HasMaxLength(200);
        builder.Property(e => e.F基础派费收费件量).HasColumnName("F基础派费收费件量");
        builder.Property(e => e.F基础派费收费金额).HasColumnName("F基础派费收费金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F基础派费收费调整金额).HasColumnName("F基础派费收费调整金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F正常派件退费收金额).HasColumnName("F正常派件退费收金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F周期性派费收金额).HasColumnName("F周期性派费收金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F大货计重收费金额).HasColumnName("F大货计重收费金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F违规重量罚款收件量).HasColumnName("F违规重量罚款收件量");
        builder.Property(e => e.F违规重量罚款收金额).HasColumnName("F违规重量罚款收金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F基础派费和时效拦截弃件付费件量合计).HasColumnName("F基础派费和时效拦截弃件付费件量合计");
        builder.Property(e => e.F基础派费和时效拦截弃件付费金额合计).HasColumnName("F基础派费和时效拦截弃件付费金额合计").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F综合KPI奖励派费件量).HasColumnName("F综合KPI奖励派费件量");
        builder.Property(e => e.F综合KPI奖励派费金额).HasColumnName("F综合KPI奖励派费金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F考核奖励派费金额).HasColumnName("F考核奖励派费金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F补贴派费付费金额).HasColumnName("F补贴派费付费金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F周期性派费付费金额).HasColumnName("F周期性派费付费金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F大货计重付费金额).HasColumnName("F大货计重付费金额").HasColumnType("decimal(18,4)");

        // ===== 索引（实际由迁移 DDL 创建，此处声明保模型一致）=====
        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通派件日明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通派件日明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");
        builder.HasIndex(e => new { e.FOrgId, e.F结算日期, e.F网点编号, e.F业务员编码 })
            .IsUnique().HasFilter("[FIsRevoked] = 0 AND [F网点编号] IS NOT NULL AND [F业务员编码] IS NOT NULL")
            .HasDatabaseName("UX_STG申通派件日明细_去重");
        builder.HasIndex(e => new { e.FOrgId, e.F结算日期, e.F归属网点编号 })
            .IncludeProperties(e => e.F基础派费收费件量)
            .HasDatabaseName("IX_STG申通派件日明细_取数");
    }
}
