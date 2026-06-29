using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAccountSetConfiguration : IEntityTypeConfiguration<FinAccountSet>
{
    public void Configure(EntityTypeBuilder<FinAccountSet> builder)
    {
        builder.ToTable("FIN账套");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50);
        builder.Property(e => e.FCompanyName).HasColumnName("F法人名称").HasMaxLength(100);
        builder.Property(e => e.FDescription).HasColumnName("F说明").HasMaxLength(500);
        builder.Property(e => e.FIsDefault).HasColumnName("F是否默认");
        builder.Property(e => e.FStatus).HasColumnName("F状态");
        builder.Property(e => e.FSortOrder).HasColumnName("F排序");
        builder.Property(e => e.FStartYear).HasColumnName("F起始年份").HasDefaultValue(0);
        builder.Property(e => e.FStartMonth).HasColumnName("F起始月份").HasDefaultValue(0);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        
        builder.HasIndex(e => e.FCode).IsUnique().HasDatabaseName("IX_FIN账套_编码");
        builder.HasIndex(e => e.FIsDefault).HasDatabaseName("IX_FIN账套_是否默认");
    }
}
