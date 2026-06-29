using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.PPV.Entities;

namespace STOTOP.Module.PPV.Configurations;

public class PpvViolationConfiguration : IEntityTypeConfiguration<PpvViolation>
{
    public void Configure(EntityTypeBuilder<PpvViolation> builder)
    {
        builder.ToTable("PPV违规记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F员工ID).HasColumnName("F员工ID");
        builder.Property(e => e.F期间).HasColumnName("F期间").HasMaxLength(6).IsRequired();
        builder.Property(e => e.F违规类型).HasColumnName("F违规类型");
        builder.Property(e => e.F关联单据ID).HasColumnName("F关联单据ID").HasMaxLength(64).IsRequired();
        builder.Property(e => e.F清零金额).HasColumnName("F清零金额").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F处理状态).HasColumnName("F处理状态").HasDefaultValue(0);
        builder.Property(e => e.F备注).HasColumnName("F备注").HasMaxLength(512);
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F员工ID, e.F期间, e.F违规类型, e.F关联单据ID })
            .IsUnique()
            .HasDatabaseName("UQ_PPV违规_组织_员工_期间_类型_单据");
    }
}
