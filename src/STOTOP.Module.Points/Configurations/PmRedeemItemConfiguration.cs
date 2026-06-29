using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Points.Entities;

namespace STOTOP.Module.Points.Configurations;

public class PmRedeemItemConfiguration : IEntityTypeConfiguration<PmRedeemItem>
{
    public void Configure(EntityTypeBuilder<PmRedeemItem> builder)
    {
        builder.ToTable("PM兑换商品");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(64).IsRequired();
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FCategory).HasColumnName("F分类").HasDefaultValue(0);
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FImage).HasColumnName("F图片").HasMaxLength(200);
        builder.Property(e => e.FRequiredPoints).HasColumnName("F所需积分");
        builder.Property(e => e.FStock).HasColumnName("F库存").HasDefaultValue(-1);
        builder.Property(e => e.FRedeemedCount).HasColumnName("F已兑换数").HasDefaultValue(0);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FSortOrder).HasColumnName("F排序").HasDefaultValue(0);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => new { e.FOrgId, e.FStatus }).HasDatabaseName("IX_PM兑换商品_组织_状态");
    }
}
