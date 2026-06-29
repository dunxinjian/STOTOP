using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAssetCardConfiguration : IEntityTypeConfiguration<FinAssetCard>
{
    public void Configure(EntityTypeBuilder<FinAssetCard> builder)
    {
        builder.ToTable("FIN资产卡片");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50);
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(200);
        builder.Property(e => e.FCategoryId).HasColumnName("F类别ID");
        builder.Property(e => e.FDepartmentId).HasColumnName("F部门ID");
        builder.Property(e => e.FOriginalValue).HasColumnName("F原值").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FAccumulatedDepreciation).HasColumnName("F累计折旧").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FNetValue).HasColumnName("F净值").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FEntryDate).HasColumnName("F入账日期");
        builder.Property(e => e.FStartDepreciationDate).HasColumnName("F开始折旧日期");
        builder.Property(e => e.FUsefulLife).HasColumnName("F使用年限");
        builder.Property(e => e.FResidualRate).HasColumnName("F残值率").HasColumnType("DECIMAL(5,2)");
        builder.Property(e => e.FStatus).HasColumnName("F状态");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        
        builder.HasIndex(e => e.FCode).IsUnique().HasDatabaseName("IX_FIN资产卡片_编码");
        builder.HasIndex(e => e.FCategoryId).HasDatabaseName("IX_FIN资产卡片_类别ID");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_FIN资产卡片_状态");
        builder.HasIndex(e => e.FAccountSetId).HasDatabaseName("IX_FIN资产卡片_账套ID");
        
        builder.HasOne<FinAssetCategory>()
            .WithMany()
            .HasForeignKey(e => e.FCategoryId)
            .HasConstraintName("FK_FIN资产卡片_类别ID");
    }
}
