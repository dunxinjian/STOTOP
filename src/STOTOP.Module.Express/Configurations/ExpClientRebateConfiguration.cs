using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpClientRebateConfiguration : IEntityTypeConfiguration<ExpClientRebate>
{
    public void Configure(EntityTypeBuilder<ExpClientRebate> builder)
    {
        builder.ToTable("EXP客户返利");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FRebateName).HasColumnName("F返利名称").HasMaxLength(50);
        builder.Property(e => e.FRebateCycle).HasColumnName("F返利周期").HasDefaultValue(1);
        builder.Property(e => e.FCalcMethod).HasColumnName("F计算方式");
        builder.Property(e => e.FFixedAmount).HasColumnName("F固定金额").HasColumnType("decimal(10,4)");
        builder.Property(e => e.FRatio).HasColumnName("F比例").HasColumnType("decimal(5,4)");
        builder.Property(e => e.FWeightPrice).HasColumnName("F重量单价").HasColumnType("decimal(10,4)");
        builder.Property(e => e.FMinTickets).HasColumnName("F最低票数");
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期");
        builder.Property(e => e.FExpiryDate).HasColumnName("F失效日期");
        builder.Property(e => e.FIsActive).HasColumnName("F启用").HasDefaultValue(true);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(200);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => new { e.FBusinessObjectId, e.FBrandCode, e.FIsActive })
            .HasDatabaseName("IX_EXP客户返利_业务对象品牌启用");
    }
}
