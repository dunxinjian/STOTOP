using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAmoebaManualClassifyConfiguration : IEntityTypeConfiguration<FinAmoebaManualClassify>
{
    public void Configure(EntityTypeBuilder<FinAmoebaManualClassify> builder)
    {
        builder.ToTable("FIN阿米巴手工分类");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FVoucherEntryId).HasColumnName("F凭证分录ID");
        builder.Property(e => e.FPLItemId).HasColumnName("F损益项ID");
        builder.Property(e => e.FMarkedTime).HasColumnName("F标记时间").HasColumnType("datetime2").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FMarkedBy).HasColumnName("F标记人").HasMaxLength(50);
        builder.Property(e => e.FSummaryPattern).HasColumnName("F摘要模式").HasMaxLength(200);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(192L);
        
        builder.HasIndex(e => e.FVoucherEntryId)
            .IsUnique()
            .HasDatabaseName("IX_FIN阿米巴手工分类_分录");
    }
}
