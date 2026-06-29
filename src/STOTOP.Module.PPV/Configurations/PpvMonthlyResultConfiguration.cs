using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.PPV.Entities;

namespace STOTOP.Module.PPV.Configurations;

public class PpvMonthlyResultConfiguration : IEntityTypeConfiguration<PpvMonthlyResult>
{
    public void Configure(EntityTypeBuilder<PpvMonthlyResult> builder)
    {
        builder.ToTable("PPV月度汇总");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F员工ID).HasColumnName("F员工ID");
        builder.Property(e => e.F期间).HasColumnName("F期间").HasMaxLength(6).IsRequired();
        builder.Property(e => e.F总产值).HasColumnName("F总产值").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F本岗产值).HasColumnName("F本岗产值").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F跨岗产值).HasColumnName("F跨岗产值").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F综合质量等级).HasColumnName("F综合质量等级");
        builder.Property(e => e.F是否跨岗清零).HasColumnName("F是否跨岗清零").HasDefaultValue(false);
        builder.Property(e => e.F清零原因).HasColumnName("F清零原因").HasMaxLength(256);
        builder.Property(e => e.FB分变化).HasColumnName("FB分变化").HasDefaultValue(0);
        builder.Property(e => e.FA分变化).HasColumnName("FA分变化").HasDefaultValue(0);
        builder.Property(e => e.F岗位ID快照).HasColumnName("F岗位ID快照");
        builder.Property(e => e.F部门ID快照).HasColumnName("F部门ID快照");
        builder.Property(e => e.F状态).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F员工ID, e.F期间 })
            .IsUnique()
            .HasDatabaseName("UQ_PPV月度汇总_员工_期间");
    }
}
