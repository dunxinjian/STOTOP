using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpBrandConfiguration : IEntityTypeConfiguration<ExpBrand>
{
    public void Configure(EntityTypeBuilder<ExpBrand> builder)
    {
        builder.ToTable("EXP品牌");

        builder.HasKey(e => e.FCode);
        builder.Property(e => e.FCode).HasColumnName("F编码").HasColumnType("NCHAR(2)").IsRequired();
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

    }
}
