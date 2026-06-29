using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpMonthlyAdjustmentConfiguration : IEntityTypeConfiguration<ExpMonthlyAdjustment>
{
    public void Configure(EntityTypeBuilder<ExpMonthlyAdjustment> builder)
    {
        builder.ToTable("EXP月度调整");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FMonth).HasColumnName("F月份");
        builder.Property(e => e.FAdjustType).HasColumnName("F调整类型");
        builder.Property(e => e.FReason).HasColumnName("F原因").HasMaxLength(200);
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasColumnType("decimal(14,2)");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => new { e.FBusinessObjectId, e.FMonth })
            .HasDatabaseName("IX_EXP月度调整_业务对象月份");
    }
}
