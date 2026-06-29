using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAuxMappingDetailConfiguration : IEntityTypeConfiguration<FinAuxMappingDetail>
{
    public void Configure(EntityTypeBuilder<FinAuxMappingDetail> builder)
    {
        builder.ToTable("F账套迁移_辅助映射");
        builder.HasKey(e => e.FID);

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.F方案ID).HasColumnName("F方案ID");
        builder.Property(e => e.F辅助类型).HasColumnName("F辅助类型").HasMaxLength(50);
        builder.Property(e => e.F源编码).HasColumnName("F源编码").HasMaxLength(100);
        builder.Property(e => e.F源名称).HasColumnName("F源名称").HasMaxLength(200);
        builder.Property(e => e.F目标辅助项目ID).HasColumnName("F目标辅助项目ID");
        builder.Property(e => e.F目标编码).HasColumnName("F目标编码").HasMaxLength(100);
        builder.Property(e => e.F目标名称).HasColumnName("F目标名称").HasMaxLength(200);
        builder.Property(e => e.F处理策略).HasColumnName("F处理策略").HasMaxLength(20);
        builder.Property(e => e.F状态).HasColumnName("F状态");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间");

        builder.HasIndex(e => e.F方案ID).HasDatabaseName("IX_F账套迁移_辅助映射_方案ID");
        builder.HasIndex(e => new { e.F辅助类型, e.F源编码 }).HasDatabaseName("IX_F账套迁移_辅助映射_类型编码");
    }
}
