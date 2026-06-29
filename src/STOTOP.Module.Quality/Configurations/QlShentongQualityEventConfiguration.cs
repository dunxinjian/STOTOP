using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Configurations;

public class QlShentongQualityEventConfiguration : IEntityTypeConfiguration<QlShentongQualityEvent>
{
    public void Configure(EntityTypeBuilder<QlShentongQualityEvent> builder)
    {
        builder.ToTable("QL申通_承运商质量事件");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("FOrgId");
        builder.Property(e => e.F承运商).HasColumnName("F承运商").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F业务日期).HasColumnName("F业务日期");
        builder.Property(e => e.F统计年月).HasColumnName("F统计年月").HasMaxLength(20);
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F网点编码).HasColumnName("F网点编码").HasMaxLength(50);
        builder.Property(e => e.F网点名称).HasColumnName("F网点名称").HasMaxLength(200);
        builder.Property(e => e.F员工工号).HasColumnName("F员工工号").HasMaxLength(50);
        builder.Property(e => e.F员工姓名原文).HasColumnName("F员工姓名原文").HasMaxLength(200);
        builder.Property(e => e.F员工ID).HasColumnName("F员工ID");
        builder.Property(e => e.F电商平台).HasColumnName("F电商平台").HasMaxLength(100);
        builder.Property(e => e.F质量域).HasColumnName("F质量域").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F问题类型编码).HasColumnName("F问题类型编码").HasMaxLength(50);
        builder.Property(e => e.F问题类型名称).HasColumnName("F问题类型名称").HasMaxLength(200);
        builder.Property(e => e.F严重度).HasColumnName("F严重度");
        builder.Property(e => e.F是否考核件).HasColumnName("F是否考核件");
        builder.Property(e => e.F考核金额).HasColumnName("F考核金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.F责任方).HasColumnName("F责任方").HasMaxLength(100);
        builder.Property(e => e.F来源STG表).HasColumnName("F来源STG表").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F来源行ID).HasColumnName("F来源行ID");
        builder.Property(e => e.F来源批次ID).HasColumnName("F来源批次ID");
        builder.Property(e => e.F关键字段JSON).HasColumnName("F关键字段JSON").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F网点匹配状态).HasColumnName("F网点匹配状态");
        builder.Property(e => e.F员工匹配状态).HasColumnName("F员工匹配状态");
        builder.Property(e => e.F是否已提升异常单).HasColumnName("F是否已提升异常单");
        builder.Property(e => e.F关联异常单ID).HasColumnName("F关联异常单ID");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        // 源行唯一索引（防重复归一同一 STG 行）
        builder.HasIndex(e => new { e.FOrgId, e.F承运商, e.F来源STG表, e.F来源行ID })
            .IsUnique()
            .HasDatabaseName("UX_QL申通_质量事件_源行");

        // 查询索引
        builder.HasIndex(e => new { e.F承运商, e.F业务日期, e.F网点编码 })
            .HasDatabaseName("IX_QL申通_质量事件_承运商_日期_网点");
        builder.HasIndex(e => new { e.F员工工号, e.F业务日期 })
            .HasDatabaseName("IX_QL申通_质量事件_员工_日期");
        builder.HasIndex(e => e.F运单号)
            .HasDatabaseName("IX_QL申通_质量事件_运单号");
        builder.HasIndex(e => new { e.F质量域, e.F问题类型编码 })
            .HasDatabaseName("IX_QL申通_质量事件_质量域_问题编码");
        builder.HasIndex(e => e.F网点匹配状态)
            .HasDatabaseName("IX_QL申通_质量事件_网点匹配状态");
        builder.HasIndex(e => e.F员工匹配状态)
            .HasDatabaseName("IX_QL申通_质量事件_员工匹配状态");
    }
}
