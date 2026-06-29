using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAmoebaManualDataConfiguration : IEntityTypeConfiguration<FinAmoebaManualData>
{
    public void Configure(EntityTypeBuilder<FinAmoebaManualData> builder)
    {
        builder.ToTable("FIN阿米巴手工数据");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTemplateId).HasColumnName("F模板ID");
        builder.Property(e => e.FPLItemId).HasColumnName("F损益项ID").IsRequired(false);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FPeriod).HasColumnName("F期间").HasMaxLength(20).IsRequired();
        // [批次5-S3] 期间键(粒度前缀+期间)。可空：SchemaAutoSync 对存量表加 NULL 列安全，迁移 V7 回填。
        // 唯一索引仍保持 FPeriod（各粒度期间串本就唯一），不纳入 FPeriodKey——避免管线在 Seeder 回填前
        // 用全 NULL 的 FPeriodKey 建唯一索引而撞 NULL 重复。
        builder.Property(e => e.FPeriodKey).HasColumnName("F期间键").HasMaxLength(20);
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.FPerUnitValue).HasColumnName("F单票均值").HasColumnType("decimal(18,4)");
        builder.Property(e => e.FDataType)
            .HasColumnName("F数据类型")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("manual");
        builder.Property(e => e.FAccountCode).HasColumnName("F科目编码").HasMaxLength(50);
        builder.Property(e => e.FAuxiliaryJson).HasColumnName("F辅助核算Json");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        // 过滤唯一索引：仅对 manual 数据强制唯一（estimate 数据允许同 PLItemId/Period 多条暂估明细）
        builder.HasIndex(e => new { e.FTemplateId, e.FPLItemId, e.FOrgId, e.FPeriod })
            .IsUnique()
            .HasFilter("[F数据类型] = 'manual'")
            .HasDatabaseName("IX_FinAmoebaManualData_Unique");
    }
}
