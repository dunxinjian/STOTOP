using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpPrepaymentBalanceConfiguration : IEntityTypeConfiguration<ExpPrepaymentBalance>
{
    public void Configure(EntityTypeBuilder<ExpPrepaymentBalance> builder)
    {
        builder.ToTable("EXP预付款余额");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_EXP预付款余额_租户ID");
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FBalance).HasColumnName("F余额").HasPrecision(14, 2).HasDefaultValue(0m);
        builder.Property(e => e.FTotalRecharge).HasColumnName("F累计充值").HasPrecision(14, 2).HasDefaultValue(0m);
        builder.Property(e => e.FTotalConsume).HasColumnName("F累计消费").HasPrecision(14, 2).HasDefaultValue(0m);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.Property(e => e.FRowVersion).HasColumnName("FRowVersion")
            .IsRowVersion();

        builder.HasIndex(e => e.FBusinessObjectId).IsUnique()
            .HasDatabaseName("IX_EXP预付款余额_F业务对象ID");
    }
}
