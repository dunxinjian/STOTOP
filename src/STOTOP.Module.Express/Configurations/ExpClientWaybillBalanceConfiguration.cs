using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpClientWaybillBalanceConfiguration : IEntityTypeConfiguration<ExpClientWaybillBalance>
{
    public void Configure(EntityTypeBuilder<ExpClientWaybillBalance> builder)
    {
        builder.ToTable("EXP客户运单号余额");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FAvailable).HasColumnName("F可用数量").HasDefaultValue(0);
        builder.Property(e => e.FUsed).HasColumnName("F已用数量").HasDefaultValue(0);
        builder.Property(e => e.FTotalAllocated).HasColumnName("F累计分配").HasDefaultValue(0);
        builder.Property(e => e.FTotalReturned).HasColumnName("F累计回收").HasDefaultValue(0);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FBusinessObjectId, e.FBrandCode }).IsUnique()
            .HasDatabaseName("IX_EXP客户运单号余额_业务对象品牌");
    }
}
