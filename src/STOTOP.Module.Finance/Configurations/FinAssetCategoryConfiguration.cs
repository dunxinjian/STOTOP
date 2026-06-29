using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAssetCategoryConfiguration : IEntityTypeConfiguration<FinAssetCategory>
{
    public void Configure(EntityTypeBuilder<FinAssetCategory> builder)
    {
        builder.ToTable("FIN资产类别");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50);
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FDepreciationMethod).HasColumnName("F折旧方法").HasMaxLength(50);
        builder.Property(e => e.FUsefulLife).HasColumnName("F使用年限");
        builder.Property(e => e.FResidualRate).HasColumnName("F残值率").HasColumnType("DECIMAL(5,2)");
        builder.Property(e => e.FDepreciationAccountId).HasColumnName("F对应折旧科目ID");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        
        builder.HasIndex(e => e.FCode).IsUnique().HasDatabaseName("IX_FIN资产类别_编码");
        builder.HasIndex(e => new { e.FAccountSetId, e.FCode }).HasDatabaseName("IX_FIN资产类别_账套编码");
        
        builder.HasOne<FinAccount>()
            .WithMany()
            .HasForeignKey(e => e.FDepreciationAccountId)
            .HasConstraintName("FK_FIN资产类别_折旧科目ID");
    }
}
