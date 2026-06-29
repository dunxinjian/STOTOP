using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Configurations;

public class QlShentongNetworkDailyMetricConfiguration : IEntityTypeConfiguration<QlShentongNetworkDailyMetric>
{
    public void Configure(EntityTypeBuilder<QlShentongNetworkDailyMetric> builder)
    {
        builder.ToTable("QL申通_网点日质量指标");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("FOrgId");
        builder.Property(e => e.F承运商).HasColumnName("F承运商").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F业务日期).HasColumnName("F业务日期");
        builder.Property(e => e.F统计年月).HasColumnName("F统计年月").HasMaxLength(20);
        builder.Property(e => e.F网点编码).HasColumnName("F网点编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F网点名称).HasColumnName("F网点名称").HasMaxLength(200);
        builder.Property(e => e.F片区).HasColumnName("F片区").HasMaxLength(100);
        builder.Property(e => e.F省区).HasColumnName("F省区").HasMaxLength(100);

        // 物流信息上传/缺失/准确
        builder.Property(e => e.F揽收上传不及时率).HasColumnName("F揽收上传不及时率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F派件上传不及时率).HasColumnName("F派件上传不及时率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F签收上传不及时率).HasColumnName("F签收上传不及时率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F揽收缺失率).HasColumnName("F揽收缺失率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F派件缺失率).HasColumnName("F派件缺失率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F到件缺失率).HasColumnName("F到件缺失率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F不准确率).HasColumnName("F不准确率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F到件不准确率).HasColumnName("F到件不准确率").HasColumnType("decimal(9,4)");

        // 揽收
        builder.Property(e => e.F及时揽收率).HasColumnName("F及时揽收率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F未及时揽收量).HasColumnName("F未及时揽收量");

        // 出仓
        builder.Property(e => e.F一频次出仓及时率).HasColumnName("F一频次出仓及时率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F未及时出仓量).HasColumnName("F未及时出仓量");
        builder.Property(e => e.F出仓预估考核金额).HasColumnName("F出仓预估考核金额").HasColumnType("decimal(18,2)");

        // 滞留
        builder.Property(e => e.F滞留率).HasColumnName("F滞留率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F考核滞留量).HasColumnName("F考核滞留量");
        builder.Property(e => e.F滞留预估考核金额).HasColumnName("F滞留预估考核金额").HasColumnType("decimal(18,2)");

        // 签收
        builder.Property(e => e.F一阶段及时签收率).HasColumnName("F一阶段及时签收率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F二阶段及时签收率).HasColumnName("F二阶段及时签收率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F当天及时签收率).HasColumnName("F当天及时签收率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F派送预估考核金额).HasColumnName("F派送预估考核金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.F有偿派费金额).HasColumnName("F有偿派费金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.F预计返款金额).HasColumnName("F预计返款金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.F48h签收率).HasColumnName("F48h签收率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F签收率考核金额).HasColumnName("F签收率考核金额").HasColumnType("decimal(18,2)");

        // 积压
        builder.Property(e => e.F日均出港量).HasColumnName("F日均出港量");
        builder.Property(e => e.F日均进港量).HasColumnName("F日均进港量");
        builder.Property(e => e.F积压倍数).HasColumnName("F积压倍数").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F超3天积压量).HasColumnName("F超3天积压量");
        builder.Property(e => e.F超5天积压量).HasColumnName("F超5天积压量");
        builder.Property(e => e.F超7天积压量).HasColumnName("F超7天积压量");

        // 遗失
        builder.Property(e => e.F遗失率ppm).HasColumnName("F遗失率ppm").HasColumnType("decimal(18,2)");
        builder.Property(e => e.F遗失量).HasColumnName("F遗失量");

        // 进港投诉 / 虚签
        builder.Property(e => e.F进港投诉量).HasColumnName("F进港投诉量");
        builder.Property(e => e.F进港投诉率).HasColumnName("F进港投诉率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F虚签投诉率).HasColumnName("F虚签投诉率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F7日虚签投诉量).HasColumnName("F7日虚签投诉量");

        // 拦截 / 转出
        builder.Property(e => e.F应拦截量).HasColumnName("F应拦截量");
        builder.Property(e => e.F拦截成功率).HasColumnName("F拦截成功率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F及时转出率).HasColumnName("F及时转出率").HasColumnType("decimal(9,4)");

        // 渗透 / 建站 / 喵柜
        builder.Property(e => e.F自建渗透率).HasColumnName("F自建渗透率").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F渗透率目标).HasColumnName("F渗透率目标").HasColumnType("decimal(9,4)");
        builder.Property(e => e.F建站待完成).HasColumnName("F建站待完成");
        builder.Property(e => e.F喵柜激活格口数).HasColumnName("F喵柜激活格口数");

        builder.Property(e => e.F考核金额合计).HasColumnName("F考核金额合计").HasColumnType("decimal(18,2)");
        builder.Property(e => e.F来源批次ID).HasColumnName("F来源批次ID");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F承运商, e.F业务日期, e.F网点编码 })
            .IsUnique()
            .HasDatabaseName("UX_QL申通_网点日质量指标_日期网点");
    }
}
