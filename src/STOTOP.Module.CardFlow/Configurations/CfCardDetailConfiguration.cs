using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfCardDetailConfiguration : IEntityTypeConfiguration<CfCardDetail>
{
    public void Configure(EntityTypeBuilder<CfCardDetail> builder)
    {
        builder.ToTable("CF实例明细");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCardId).HasColumnName("F卡片ID");
        builder.Property(e => e.FDetailTableKey)
            .HasColumnName("F明细表键")
            .HasMaxLength(80)
            .HasDefaultValue("default");
        builder.Property(e => e.FSortOrder).HasColumnName("F排序号");
        builder.Property(e => e.FDataJson).HasColumnName("F数据JSON");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => new { e.FCardId, e.FDetailTableKey, e.FSortOrder })
            .HasDatabaseName("IX_CF实例明细_卡片_表键_排序");
    }
}
