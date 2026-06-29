using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfIncomeConfiguration : IEntityTypeConfiguration<ConfIncome>
{
    public void Configure(EntityTypeBuilder<ConfIncome> builder)
    {
        builder.ToTable("CONF收入登记");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FEventId).HasColumnName("F活动ID");
        builder.Property(e => e.FAttendeeId).HasColumnName("F参会人ID");
        builder.Property(e => e.FType).HasColumnName("F类型").HasMaxLength(20);
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FPaymentMethod).HasColumnName("F支付方式").HasMaxLength(20);
        builder.Property(e => e.FPayerName).HasColumnName("F缴费人姓名").HasMaxLength(50);
        builder.Property(e => e.FPayerOrganization).HasColumnName("F缴费人单位").HasMaxLength(200);
        builder.Property(e => e.FPaymentDate).HasColumnName("F收款日期").HasColumnType("date");
        builder.Property(e => e.FReceiptNumber).HasColumnName("F收据编号").HasMaxLength(50);
        builder.Property(e => e.FRemark).HasColumnName("F备注");
        builder.Property(e => e.FRegistrant).HasColumnName("F登记人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasOne(e => e.Attendee)
            .WithMany(e => e.Incomes)
            .HasForeignKey(e => e.FAttendeeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.FEventId).HasDatabaseName("IX_CONF收入登记_活动ID");
        builder.HasIndex(e => e.FType).HasDatabaseName("IX_CONF收入登记_类型");
    }
}
