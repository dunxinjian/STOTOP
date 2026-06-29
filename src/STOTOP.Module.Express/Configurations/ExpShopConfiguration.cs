using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpShopConfiguration : IEntityTypeConfiguration<ExpShop>
{
    public void Configure(EntityTypeBuilder<ExpShop> builder)
    {
        builder.ToTable("EXP店铺");

        builder.HasKey(e => e.FName);
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FPlatform).HasColumnName("F平台").HasMaxLength(50);
        builder.Property(e => e.FIsShared).HasColumnName("F是否共享").HasDefaultValue(false);
        builder.Property(e => e.FIsAutoCreated).HasColumnName("F是否自动创建").HasDefaultValue(false);
        builder.Property(e => e.FNeedsAssignment).HasColumnName("F待关联").HasDefaultValue(false);
        builder.Property(e => e.FContactName).HasColumnName("F联系人").HasMaxLength(50);
        builder.Property(e => e.FContactPhone).HasColumnName("F联系电话").HasMaxLength(20);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        // [Obsolete] Assignments 导航属性已废弃，关系配置一并注释
        // builder.HasMany(e => e.Assignments)
        //     .WithOne()
        //     .HasForeignKey(a => a.FShopName)
        //     .OnDelete(DeleteBehavior.Cascade);
    }
}
