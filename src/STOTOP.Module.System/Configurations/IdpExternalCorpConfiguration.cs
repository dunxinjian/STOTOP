using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class IdpExternalCorpConfiguration : IEntityTypeConfiguration<IdpExternalCorp>
{
    public void Configure(EntityTypeBuilder<IdpExternalCorp> builder)
    {
        builder.ToTable("IDP外部企业");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FProvider).HasColumnName("F供应商");
        builder.Property(e => e.FCorpId).HasColumnName("F企业CorpId").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FAccessConfig).HasColumnName("F接入配置");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FCorpId).IsUnique().HasDatabaseName("UQ_IDP外部企业_CorpId");
    }
}
