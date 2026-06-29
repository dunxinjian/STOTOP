using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAccountPeriodConfiguration : IEntityTypeConfiguration<FinAccountPeriod>
{
    public void Configure(EntityTypeBuilder<FinAccountPeriod> builder)
    {
        builder.ToTable("FIN会计期间");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FYear).HasColumnName("F年度");
        builder.Property(e => e.FPeriodNo).HasColumnName("F期间号");
        builder.Property(e => e.FStartDate).HasColumnName("F开始日期");
        builder.Property(e => e.FEndDate).HasColumnName("F结束日期");
        builder.Property(e => e.FIsClosed).HasColumnName("F是否结账");
        builder.Property(e => e.FStatus).HasColumnName("F状态");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID").HasDefaultValue(0L);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        
        builder.HasIndex(e => e.FAccountSetId).HasDatabaseName("IX_FIN会计期间_账套ID");
        builder.HasIndex(e => new { e.FAccountSetId, e.FYear, e.FPeriodNo }).IsUnique().HasDatabaseName("IX_FIN会计期间_年度期间");
    }
}
