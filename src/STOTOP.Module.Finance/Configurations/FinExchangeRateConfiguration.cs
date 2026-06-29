using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinExchangeRateConfiguration : IEntityTypeConfiguration<FinExchangeRate>
{
    public void Configure(EntityTypeBuilder<FinExchangeRate> builder)
    {
        builder.ToTable("FIN汇率");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FCurrencyCode).HasColumnName("F币种代码").HasColumnType("NVARCHAR(10)");
        builder.Property(e => e.FCurrencyName).HasColumnName("F币种名称").HasColumnType("NVARCHAR(50)");
        builder.Property(e => e.FRate).HasColumnName("F汇率").HasColumnType("DECIMAL(18,6)");
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间");

        builder.HasIndex(e => new { e.FAccountSetId, e.FCurrencyCode, e.FEffectiveDate })
            .HasDatabaseName("IX_FIN汇率_账套_币种_日期");
    }
}
