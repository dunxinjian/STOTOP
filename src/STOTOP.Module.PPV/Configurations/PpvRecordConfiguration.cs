using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.PPV.Entities;

namespace STOTOP.Module.PPV.Configurations;

public class PpvRecordConfiguration : IEntityTypeConfiguration<PpvRecord>
{
    public void Configure(EntityTypeBuilder<PpvRecord> builder)
    {
        builder.ToTable("PPV产值记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F员工ID).HasColumnName("F员工ID");
        builder.Property(e => e.F期间).HasColumnName("F期间").HasMaxLength(6).IsRequired();
        builder.Property(e => e.F模板ID).HasColumnName("F模板ID");
        builder.Property(e => e.F产值项编码).HasColumnName("F产值项编码").HasMaxLength(64).IsRequired();
        builder.Property(e => e.F数量).HasColumnName("F数量").HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(e => e.F产值金额).HasColumnName("F产值金额").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F质量等级).HasColumnName("F质量等级");
        builder.Property(e => e.F是否跨岗).HasColumnName("F是否跨岗").HasDefaultValue(false);
        builder.Property(e => e.F审核状态).HasColumnName("F审核状态").HasDefaultValue(0);
        builder.Property(e => e.F审核人ID).HasColumnName("F审核人ID");
        builder.Property(e => e.F审核时间).HasColumnName("F审核时间");
        builder.Property(e => e.F审核备注).HasColumnName("F审核备注").HasMaxLength(512);
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F员工ID, e.F期间 })
            .HasDatabaseName("IX_PPV产值记录_组织_员工_期间");
    }
}
