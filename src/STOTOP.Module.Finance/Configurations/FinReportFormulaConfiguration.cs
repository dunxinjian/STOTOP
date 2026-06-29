using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinReportFormulaConfiguration : IEntityTypeConfiguration<FinReportFormula>
{
    public void Configure(EntityTypeBuilder<FinReportFormula> builder)
    {
        builder.ToTable("FIN报表公式");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FReportType).HasColumnName("FReportType").HasMaxLength(50);
        builder.Property(e => e.FItemName).HasColumnName("FItemName").HasMaxLength(200);
        builder.Property(e => e.FRowIndex).HasColumnName("FRowIndex");
        builder.Property(e => e.FFormula).HasColumnName("FFormula");
        builder.Property(e => e.FFormulaType).HasColumnName("FFormulaType").HasMaxLength(20);
        builder.Property(e => e.FAccountCodes).HasColumnName("FAccountCodes");
        builder.Property(e => e.FDisplayConfig).HasColumnName("FDisplayConfig");
        builder.Property(e => e.FIsEnabled).HasColumnName("FIsEnabled").HasDefaultValue(true);
        builder.Property(e => e.FAccountSetId).HasColumnName("FAccountSetId").HasDefaultValue(0L);
        builder.Property(e => e.FSortOrder).HasColumnName("FSortOrder").HasDefaultValue(0);
        builder.Property(e => e.FCreatedTime).HasColumnName("FCreatedTime");
        builder.Property(e => e.FUpdatedTime).HasColumnName("FUpdatedTime");

        builder.HasIndex(e => e.FAccountSetId).HasDatabaseName("IX_FIN报表公式_AccountSetId");
        builder.HasIndex(e => e.FReportType).HasDatabaseName("IX_FIN报表公式_ReportType");
    }
}
