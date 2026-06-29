using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Contract.Entities;

namespace STOTOP.Module.Contract.Configurations;

public class ConESignRecordConfiguration : IEntityTypeConfiguration<ConESignRecord>
{
    public void Configure(EntityTypeBuilder<ConESignRecord> builder)
    {
        builder.ToTable("CON电子签记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FContractId).HasColumnName("F合同ID");
        builder.Property(e => e.FSigner).HasColumnName("F签署人").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FSignerRole).HasColumnName("F签署角色").HasMaxLength(50);
        builder.Property(e => e.FSignMethod).HasColumnName("F签署方式").HasMaxLength(50);
        builder.Property(e => e.FSignStatus).HasColumnName("F签署状态").HasDefaultValue(0);
        builder.Property(e => e.FSignedTime).HasColumnName("F签署时间");
        builder.Property(e => e.FThirdPartyNo).HasColumnName("F第三方流水号").HasMaxLength(200);
        builder.Property(e => e.FSignedFilePath).HasColumnName("F签署文件路径").HasMaxLength(500);
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FContractId).HasDatabaseName("IX_CON电子签记录_F合同ID");
    }
}
