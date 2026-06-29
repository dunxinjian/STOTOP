using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Configurations;

public class QlAlertConfigConfiguration : IEntityTypeConfiguration<QlAlertConfig>
{
    public void Configure(EntityTypeBuilder<QlAlertConfig> builder)
    {
        builder.ToTable("QL预警配置");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.F配置名称).HasColumnName("F配置名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.F阈值类型).HasColumnName("F阈值类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F阈值).HasColumnName("F阈值").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F通知方式).HasColumnName("F通知方式").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F通知对象).HasColumnName("F通知对象");
        builder.Property(e => e.F状态).HasColumnName("F状态");
        builder.Property(e => e.FOrgId).HasColumnName("FOrgId");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
    }
}
