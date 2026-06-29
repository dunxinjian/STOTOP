using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.KSF.Entities;

namespace STOTOP.Module.KSF.Configurations;

public class KsfResultDetailConfiguration : IEntityTypeConfiguration<KsfResultDetail>
{
    public void Configure(EntityTypeBuilder<KsfResultDetail> builder)
    {
        builder.ToTable("KSF结果明细");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F结果ID).HasColumnName("F结果ID");
        builder.Property(e => e.F指标ID).HasColumnName("F指标ID");
        builder.Property(e => e.F实际值).HasColumnName("F实际值").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F差额).HasColumnName("F差额").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F金额变动).HasColumnName("F金额变动").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F指标快照JSON).HasColumnName("F指标快照JSON");

        builder.HasIndex(e => e.F结果ID).HasDatabaseName("IX_KSF结果明细_结果ID");
    }
}
