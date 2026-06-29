using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysCodeSequenceConfiguration : IEntityTypeConfiguration<SysCodeSequence>
{
    public void Configure(EntityTypeBuilder<SysCodeSequence> builder)
    {
        builder.ToTable("SYS编码序列");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FRuleId).HasColumnName("F规则ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FPeriodKey).HasColumnName("F周期标识").HasMaxLength(20).IsRequired().HasDefaultValue("");
        builder.Property(e => e.FCurrentValue).HasColumnName("F当前值").HasDefaultValue(0);
        builder.Property(e => e.FUpdateTime).HasColumnName("F修改时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FRuleId, e.FOrgId, e.FPeriodKey }).IsUnique();

        builder.HasOne(e => e.Rule)
            .WithMany(r => r.Sequences)
            .HasForeignKey(e => e.FRuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
