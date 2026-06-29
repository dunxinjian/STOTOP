using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpCostPlanItemOutletConfiguration : IEntityTypeConfiguration<ExpCostPlanItemOutlet>
{
    public void Configure(EntityTypeBuilder<ExpCostPlanItemOutlet> builder)
    {
        builder.ToTable("EXP成本方案_成本项_应用网点");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FItemId).HasColumnName("F成本项ID");
        builder.Property(e => e.FOutletId).HasColumnName("F网点ID");

        builder.HasIndex(e => new { e.FItemId, e.FOutletId })
            .IsUnique()
            .HasDatabaseName("IX_EXP成本方案_成本项_应用网点_项网点");
    }
}
