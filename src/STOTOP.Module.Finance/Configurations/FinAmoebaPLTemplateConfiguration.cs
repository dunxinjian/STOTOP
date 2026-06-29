using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAmoebaPLTemplateConfiguration : IEntityTypeConfiguration<FinAmoebaPLTemplate>
{
    public void Configure(EntityTypeBuilder<FinAmoebaPLTemplate> builder)
    {
        builder.ToTable("FIN阿米巴损益模板");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(500);
        builder.Property(e => e.FIsDefault).HasColumnName("F是否默认");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        
        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(e => e.FTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
