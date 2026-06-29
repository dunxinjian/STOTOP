using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.PPV.Entities;

namespace STOTOP.Module.PPV.Configurations;

public class PpvTemplateConfiguration : IEntityTypeConfiguration<PpvTemplate>
{
    public void Configure(EntityTypeBuilder<PpvTemplate> builder)
    {
        builder.ToTable("PPV产值模板");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F名称).HasColumnName("F名称").HasMaxLength(128).IsRequired();
        builder.Property(e => e.F岗位ID).HasColumnName("F岗位ID");
        builder.Property(e => e.F产值项编码).HasColumnName("F产值项编码").HasMaxLength(64).IsRequired();
        builder.Property(e => e.F产值项名称).HasColumnName("F产值项名称").HasMaxLength(128).IsRequired();
        builder.Property(e => e.F单价).HasColumnName("F单价").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F计量单位).HasColumnName("F计量单位").HasMaxLength(32);
        builder.Property(e => e.F启用状态).HasColumnName("F启用状态").HasDefaultValue(true);
        builder.Property(e => e.F生效起期).HasColumnName("F生效起期");
        builder.Property(e => e.F生效止期).HasColumnName("F生效止期");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.F更新时间).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F岗位ID, e.F产值项编码 })
            .IsUnique()
            .HasDatabaseName("UQ_PPV产值模板_组织_岗位_编码");
    }
}
