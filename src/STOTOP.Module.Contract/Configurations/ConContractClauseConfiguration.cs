using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Contract.Entities;

namespace STOTOP.Module.Contract.Configurations;

public class ConContractClauseConfiguration : IEntityTypeConfiguration<ConContractClause>
{
    public void Configure(EntityTypeBuilder<ConContractClause> builder)
    {
        builder.ToTable("CON合同条款");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FContractId).HasColumnName("F合同ID");
        builder.Property(e => e.FClauseOrder).HasColumnName("F条款序号").HasDefaultValue(0);
        builder.Property(e => e.FClauseTitle).HasColumnName("F条款标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FClauseContent).HasColumnName("F条款内容").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FIsKeyClause).HasColumnName("F是否关键条款").HasDefaultValue(false);
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FContractId).HasDatabaseName("IX_CON合同条款_F合同ID");
    }
}
