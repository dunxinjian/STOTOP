using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinVoucherAssetLinkConfiguration : IEntityTypeConfiguration<FinVoucherAssetLink>
{
    public void Configure(EntityTypeBuilder<FinVoucherAssetLink> builder)
    {
        builder.ToTable("FIN凭证资产关联");
        builder.HasKey(e => e.FID);

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.F凭证ID).HasColumnName("F凭证ID");
        builder.Property(e => e.F分录ID).HasColumnName("F分录ID");
        builder.Property(e => e.F资产卡片ID).HasColumnName("F资产卡片ID");
        builder.Property(e => e.F关联类型).HasColumnName("F关联类型");
        builder.Property(e => e.F批次ID).HasColumnName("F批次ID");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间");

        builder.HasIndex(e => e.F凭证ID).HasDatabaseName("IX_FIN凭证资产关联_凭证ID");
        builder.HasIndex(e => e.F资产卡片ID).HasDatabaseName("IX_FIN凭证资产关联_资产卡片ID");

        builder.HasOne<FinVoucher>()
            .WithMany()
            .HasForeignKey(e => e.F凭证ID)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_FIN凭证资产关联_凭证ID");

        builder.HasOne<FinAssetCard>()
            .WithMany()
            .HasForeignKey(e => e.F资产卡片ID)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_FIN凭证资产关联_资产卡片ID");
    }
}
