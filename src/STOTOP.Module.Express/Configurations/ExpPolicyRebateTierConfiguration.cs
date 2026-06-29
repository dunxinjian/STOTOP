using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpPolicyRebateTierConfiguration : IEntityTypeConfiguration<ExpPolicyRebateTier>
{
    public void Configure(EntityTypeBuilder<ExpPolicyRebateTier> builder)
    {
        builder.ToTable("EXP政策返利阶梯");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FPolicyRebateId).HasColumnName("F政策返利ID");
        builder.Property(e => e.FDailyVolumeFrom).HasColumnName("F日单量起");
        builder.Property(e => e.FDailyVolumeTo).HasColumnName("F日单量止");
        builder.Property(e => e.FRebatePerTicket).HasColumnName("F每票返利").HasColumnType("decimal(10,4)");
        builder.Property(e => e.FSortOrder).HasColumnName("F排序").HasDefaultValue(0);

        builder.HasIndex(e => new { e.FPolicyRebateId, e.FSortOrder })
            .HasDatabaseName("IX_EXP政策返利阶梯_政策排序");
    }
}
