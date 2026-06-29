using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpCostItemConfiguration : IEntityTypeConfiguration<ExpCostItem>
{
    public void Configure(EntityTypeBuilder<ExpCostItem> builder)
    {
        builder.ToTable("EXP成本项目");

        builder.HasKey(e => e.FID);
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FIsRebate).HasColumnName("F是否返利").HasDefaultValue(false);
        builder.Property(e => e.FSortOrder).HasColumnName("F排序").HasDefaultValue(0);
    }
}
