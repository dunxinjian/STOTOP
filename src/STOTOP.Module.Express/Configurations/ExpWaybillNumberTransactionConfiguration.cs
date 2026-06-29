using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpWaybillNumberTransactionConfiguration : IEntityTypeConfiguration<ExpWaybillNumberTransaction>
{
    public void Configure(EntityTypeBuilder<ExpWaybillNumberTransaction> builder)
    {
        builder.ToTable("EXP运单号交易");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FPoolId).HasColumnName("F号段ID");
        builder.Property(e => e.FTransactionType).HasColumnName("F交易类型");
        builder.Property(e => e.FQuantity).HasColumnName("F数量");
        builder.Property(e => e.FStartNo).HasColumnName("F起始号").HasMaxLength(30);
        builder.Property(e => e.FEndNo).HasColumnName("F截止号").HasMaxLength(30);
        builder.Property(e => e.FTransactionDate).HasColumnName("F交易日期");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => new { e.FBusinessObjectId, e.FBrandCode, e.FTransactionDate })
            .HasDatabaseName("IX_EXP运单号交易_业务对象品牌日期");
    }
}
