using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAttachmentConfiguration : IEntityTypeConfiguration<FinAttachment>
{
    public void Configure(EntityTypeBuilder<FinAttachment> builder)
    {
        builder.ToTable("FIN附件");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FBusinessType).HasColumnName("F业务类型").HasMaxLength(50);
        builder.Property(e => e.FBusinessId).HasColumnName("F业务ID");
        builder.Property(e => e.FFileName).HasColumnName("F文件名").HasMaxLength(200);
        builder.Property(e => e.FOriginalName).HasColumnName("F原始文件名").HasMaxLength(500);
        builder.Property(e => e.FFilePath).HasColumnName("F文件路径").HasMaxLength(1000);
        builder.Property(e => e.FFileSize).HasColumnName("F文件大小");
        builder.Property(e => e.FContentType).HasColumnName("F内容类型").HasMaxLength(200);
        builder.Property(e => e.FUploadTime).HasColumnName("F上传时间");
        builder.Property(e => e.FUploaderId).HasColumnName("F上传者ID");
        builder.Property(e => e.FUploaderName).HasColumnName("F上传者名称").HasMaxLength(100);

        builder.HasIndex(e => new { e.FBusinessType, e.FBusinessId })
            .HasDatabaseName("IX_FIN附件_业务类型_业务ID");

        builder.HasIndex(e => e.FAccountSetId)
            .HasDatabaseName("IX_FIN附件_账套ID");
    }
}
