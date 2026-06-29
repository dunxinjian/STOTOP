using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Contract.Entities;

namespace STOTOP.Module.Contract.Configurations;

public class ConContractTemplateConfiguration : IEntityTypeConfiguration<ConContractTemplate>
{
    public void Configure(EntityTypeBuilder<ConContractTemplate> builder)
    {
        builder.ToTable("CON合同模板");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTypeId).HasColumnName("F类型ID");
        builder.Property(e => e.FTemplateName).HasColumnName("F模板名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FTemplateContent).HasColumnName("F模板内容").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FVersion).HasColumnName("F版本号").HasDefaultValue(1);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FTypeId).HasDatabaseName("IX_CON合同模板_F类型ID");

        builder.HasOne(e => e.Type)
            .WithMany()
            .HasForeignKey(e => e.FTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
