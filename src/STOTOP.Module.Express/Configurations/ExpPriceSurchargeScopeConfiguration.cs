using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpPriceSurchargeScopeConfiguration : IEntityTypeConfiguration<ExpPriceSurchargeScope>
{
    public void Configure(EntityTypeBuilder<ExpPriceSurchargeScope> builder)
    {
        builder.ToTable("EXP快递报价_出港加收_作用域");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("Id");
        builder.Property(e => e.FSurchargeId).HasColumnName("F加收规则ID");
        builder.Property(e => e.FLinkedType).HasColumnName("F关联类型").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FLinkedId).HasColumnName("F关联ID").HasMaxLength(50).IsRequired();
        builder.HasOne(e => e.Surcharge).WithMany(s => s.Scopes).HasForeignKey(e => e.FSurchargeId);
    }
}
