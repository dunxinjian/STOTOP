using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAuxiliaryTypeConfiguration : IEntityTypeConfiguration<FinAuxiliaryType>
{
    public void Configure(EntityTypeBuilder<FinAuxiliaryType> builder)
    {
        builder.ToTable("FIN辅助核算类型");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FStatus).HasColumnName("F状态");
        builder.Property(e => e.FScope).HasColumnName("F作用域").HasMaxLength(20).HasDefaultValue("org_scoped");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        
        builder.HasIndex(e => e.FName).IsUnique().HasDatabaseName("IX_FIN辅助核算类型_名称");
    }
}
