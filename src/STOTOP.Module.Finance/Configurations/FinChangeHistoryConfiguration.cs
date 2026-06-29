using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinChangeHistoryConfiguration : IEntityTypeConfiguration<FinChangeHistory>
{
    public void Configure(EntityTypeBuilder<FinChangeHistory> builder)
    {
        builder.ToTable("FIN变更历史");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FEntityType).HasColumnName("FEntityType").HasMaxLength(50);
        builder.Property(e => e.FEntityId).HasColumnName("FEntityId");
        builder.Property(e => e.FFieldName).HasColumnName("FFieldName").HasMaxLength(100);
        builder.Property(e => e.FOldValue).HasColumnName("FOldValue");
        builder.Property(e => e.FNewValue).HasColumnName("FNewValue");
        builder.Property(e => e.FOperatorId).HasColumnName("FOperatorId");
        builder.Property(e => e.FOperatorName).HasColumnName("FOperatorName").HasMaxLength(50);
        builder.Property(e => e.FOperationTime).HasColumnName("FOperationTime");
        builder.Property(e => e.FAccountSetId).HasColumnName("FAccountSetId").HasDefaultValue(0L);

        builder.HasIndex(e => e.FAccountSetId).HasDatabaseName("IX_FIN变更历史_AccountSetId");
        builder.HasIndex(e => new { e.FEntityType, e.FEntityId }).HasDatabaseName("IX_FIN变更历史_Entity");
    }
}
