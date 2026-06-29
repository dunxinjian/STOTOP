using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAccountTemplateConfiguration : IEntityTypeConfiguration<FinAccountTemplate>
{
    public void Configure(EntityTypeBuilder<FinAccountTemplate> builder)
    {
        builder.ToTable("FIN科目模板");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50);
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FDescription).HasColumnName("F说明").HasMaxLength(500);
        builder.Property(e => e.FIsPreset).HasColumnName("F是否预置").HasDefaultValue(0);
        builder.Property(e => e.FEnableStatus).HasColumnName("F启用状态").HasDefaultValue(1);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        
        builder.HasIndex(e => e.FCode).IsUnique().HasDatabaseName("IX_FIN科目模板_编码");
    }
}
