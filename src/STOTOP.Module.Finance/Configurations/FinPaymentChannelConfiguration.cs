using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinPaymentChannelConfiguration : IEntityTypeConfiguration<FinPaymentChannel>
{
    public void Configure(EntityTypeBuilder<FinPaymentChannel> builder)
    {
        builder.ToTable("FIN交易渠道");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FType).HasColumnName("F类型");
        builder.Property(e => e.FAccountNo).HasColumnName("F账号").HasMaxLength(100);
        builder.Property(e => e.FBankName).HasColumnName("F开户行").HasMaxLength(200);
        builder.Property(e => e.FImportTemplate).HasColumnName("F导入模板").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
    }
}
