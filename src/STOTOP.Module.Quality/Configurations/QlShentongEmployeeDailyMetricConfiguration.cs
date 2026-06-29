using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Configurations;

public class QlShentongEmployeeDailyMetricConfiguration : IEntityTypeConfiguration<QlShentongEmployeeDailyMetric>
{
    public void Configure(EntityTypeBuilder<QlShentongEmployeeDailyMetric> builder)
    {
        builder.ToTable("QL申通_员工日质量指标");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("FOrgId");
        builder.Property(e => e.F承运商).HasColumnName("F承运商").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F业务日期).HasColumnName("F业务日期");
        builder.Property(e => e.F统计年月).HasColumnName("F统计年月").HasMaxLength(20);
        builder.Property(e => e.F网点编码).HasColumnName("F网点编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F员工工号).HasColumnName("F员工工号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F员工姓名原文).HasColumnName("F员工姓名原文").HasMaxLength(200);
        builder.Property(e => e.F员工ID).HasColumnName("F员工ID");

        // 派签
        builder.Property(e => e.F派件量).HasColumnName("F派件量");
        builder.Property(e => e.F当日派签量).HasColumnName("F当日派签量");
        builder.Property(e => e.F当日派签率).HasColumnName("F当日派签率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F应上门量).HasColumnName("F应上门量");
        builder.Property(e => e.F未上门量).HasColumnName("F未上门量");
        builder.Property(e => e.F按需上门率).HasColumnName("F按需上门率").HasColumnType("decimal(9,4)");

        // 客诉
        builder.Property(e => e.F客诉发起量).HasColumnName("F客诉发起量");
        builder.Property(e => e.F工单定责量).HasColumnName("F工单定责量");
        builder.Property(e => e.F客诉发起率).HasColumnName("F客诉发起率").HasColumnType("decimal(9,4)");

        // 质检 / 时效
        builder.Property(e => e.F虚假签收数).HasColumnName("F虚假签收数");
        builder.Property(e => e.F照片质检不合格数).HasColumnName("F照片质检不合格数");
        builder.Property(e => e.F派送超时T0数).HasColumnName("F派送超时T0数");
        builder.Property(e => e.F派送超时T1数).HasColumnName("F派送超时T1数");
        builder.Property(e => e.F派送超时T2数).HasColumnName("F派送超时T2数");
        builder.Property(e => e.F派送超时T3数).HasColumnName("F派送超时T3数");
        builder.Property(e => e.F揽收不及时数).HasColumnName("F揽收不及时数");
        builder.Property(e => e.F上传不及时数).HasColumnName("F上传不及时数");
        builder.Property(e => e.F问题件数).HasColumnName("F问题件数");

        // 违规
        builder.Property(e => e.F违规虚假电联).HasColumnName("F违规虚假电联");
        builder.Property(e => e.F违规无效电联).HasColumnName("F违规无效电联");
        builder.Property(e => e.F违规双签).HasColumnName("F违规双签");
        builder.Property(e => e.F违规照片定位虚假).HasColumnName("F违规照片定位虚假");
        builder.Property(e => e.F违规签收文本不规范).HasColumnName("F违规签收文本不规范");
        builder.Property(e => e.F违规引导代收).HasColumnName("F违规引导代收");
        builder.Property(e => e.F回访真实率).HasColumnName("F回访真实率").HasColumnType("decimal(9,4)");

        builder.Property(e => e.F考核金额合计).HasColumnName("F考核金额合计").HasColumnType("decimal(18,2)");
        builder.Property(e => e.F来源批次ID).HasColumnName("F来源批次ID");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F承运商, e.F业务日期, e.F网点编码, e.F员工工号 })
            .IsUnique()
            .HasDatabaseName("UX_QL申通_员工日质量指标_日期网点员工");
    }
}
