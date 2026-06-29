using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpCityConfiguration : IEntityTypeConfiguration<ExpCity>
{
    public void Configure(EntityTypeBuilder<ExpCity> builder)
    {
        builder.ToTable("EXP城市");

        builder.HasKey(e => e.FID);
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FProvinceId).HasColumnName("F省份ID");
        builder.Property(e => e.FProvinceName).HasColumnName("F省份名").HasMaxLength(50);

        builder.HasIndex(e => e.FCode).IsUnique().HasDatabaseName("IX_EXP城市_F编码");
        builder.HasIndex(e => e.FProvinceId).HasDatabaseName("IX_EXP城市_F省份ID");
    }
}
