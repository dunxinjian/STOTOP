using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmAttachmentConfiguration : IEntityTypeConfiguration<TmAttachment>
{
    public void Configure(EntityTypeBuilder<TmAttachment> builder)
    {
        builder.ToTable("TM附件");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FRelationType).HasColumnName("F关联类型");
        builder.Property(e => e.FRelationId).HasColumnName("F关联ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FOriginalFileName).HasColumnName("F原始文件名").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FStoragePath).HasColumnName("F存储路径").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FFileSize).HasColumnName("F文件大小");
        builder.Property(e => e.FFileType).HasColumnName("F文件类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FRelationType, e.FRelationId })
            .HasDatabaseName("IX_TM附件_关联");
    }
}
