using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpWaybillNumberPoolConfiguration : IEntityTypeConfiguration<ExpWaybillNumberPool>
{
    public void Configure(EntityTypeBuilder<ExpWaybillNumberPool> builder)
    {
        builder.ToTable("EXP运单号段");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FPrefix).HasColumnName("F前缀").HasMaxLength(10);
        builder.Property(e => e.FStartNo).HasColumnName("F起始号").HasMaxLength(30);
        builder.Property(e => e.FEndNo).HasColumnName("F截止号").HasMaxLength(30);
        builder.Property(e => e.FTotalCount).HasColumnName("F总数量");
        builder.Property(e => e.FAllocated).HasColumnName("F已分配").HasDefaultValue(0);
        builder.Property(e => e.FRemaining).HasColumnName("F剩余数量")
            .HasComputedColumnSql("[F总数量] - [F已分配]", stored: true);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
    }
}
