using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinVoucherTemplateConfiguration : IEntityTypeConfiguration<FinVoucherTemplate>
{
    public void Configure(EntityTypeBuilder<FinVoucherTemplate> builder)
    {
        builder.ToTable("FIN凭证模板");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(500);
        builder.Property(e => e.FSort).HasColumnName("F排序");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间");

        builder.HasIndex(e => e.FAccountSetId).HasDatabaseName("IX_FIN凭证模板_账套ID");

        builder.HasMany(e => e.Entries)
            .WithOne(e => e.Template)
            .HasForeignKey(e => e.FTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
