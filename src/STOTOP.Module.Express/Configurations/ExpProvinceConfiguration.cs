using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpProvinceConfiguration : IEntityTypeConfiguration<ExpProvince>
{
    public void Configure(EntityTypeBuilder<ExpProvince> builder)
    {
        builder.ToTable("EXP省份");

        builder.HasKey(e => e.FID);
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(10).IsRequired();
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FShortName).HasColumnName("F简称").HasMaxLength(10).IsRequired();
        builder.Property(e => e.FRegion).HasColumnName("F大区").HasMaxLength(20);
        builder.Property(e => e.FIsRemote).HasColumnName("F偏远").HasDefaultValue(false);

        builder.HasIndex(e => e.FCode).IsUnique().HasDatabaseName("IX_EXP省份_F编码");
    }
}
