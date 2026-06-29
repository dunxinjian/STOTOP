using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Contract.Entities;

namespace STOTOP.Module.Contract.Configurations;

public class ConContractPartyConfiguration : IEntityTypeConfiguration<ConContractParty>
{
    public void Configure(EntityTypeBuilder<ConContractParty> builder)
    {
        builder.ToTable("CON合同方");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FContractId).HasColumnName("F合同ID");
        builder.Property(e => e.FPartyRole).HasColumnName("F方角色");
        builder.Property(e => e.FRelatedBusinessType).HasColumnName("F关联业务类型").HasMaxLength(50);
        builder.Property(e => e.FRelatedBusinessId).HasColumnName("F关联业务ID");
        builder.Property(e => e.FPartyName).HasColumnName("F方名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FContact).HasColumnName("F联系人").HasMaxLength(100);
        builder.Property(e => e.FPhone).HasColumnName("F电话").HasMaxLength(50);
        builder.Property(e => e.FAddress).HasColumnName("F地址").HasMaxLength(500);
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FContractId).HasDatabaseName("IX_CON合同方_F合同ID");
        builder.HasIndex(e => new { e.FRelatedBusinessType, e.FRelatedBusinessId }).HasDatabaseName("IX_CON合同方_F关联业务");
    }
}
