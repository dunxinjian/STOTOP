using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAssetMappingDetailConfiguration : IEntityTypeConfiguration<FinAssetMappingDetail>
{
    public void Configure(EntityTypeBuilder<FinAssetMappingDetail> builder)
    {
        builder.ToTable("F账套迁移_资产映射");
        builder.HasKey(e => e.FID);

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.F方案ID).HasColumnName("F方案ID");
        builder.Property(e => e.F源资产编号).HasColumnName("F源资产编号").HasMaxLength(100);
        builder.Property(e => e.F目标资产卡片ID).HasColumnName("F目标资产卡片ID");
        builder.Property(e => e.F目标资产编号).HasColumnName("F目标资产编号").HasMaxLength(100);
        builder.Property(e => e.F目标资产名称).HasColumnName("F目标资产名称").HasMaxLength(200);
        builder.Property(e => e.F状态).HasColumnName("F状态");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间");

        builder.HasIndex(e => e.F方案ID).HasDatabaseName("IX_F账套迁移_资产映射_方案ID");
        builder.HasIndex(e => e.F源资产编号).HasDatabaseName("IX_F账套迁移_资产映射_源资产编号");
    }
}
