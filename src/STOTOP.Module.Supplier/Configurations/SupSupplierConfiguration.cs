using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Supplier.Entities;

namespace STOTOP.Module.Supplier.Configurations;

public class SupSupplierConfiguration : IEntityTypeConfiguration<SupSupplier>
{
    public void Configure(EntityTypeBuilder<SupSupplier> builder)
    {
        builder.ToTable("SUP供应商");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FFullName).HasColumnName("F全称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FShortName).HasColumnName("F简称").HasMaxLength(100);
        builder.Property(e => e.FCreditCode).HasColumnName("F统一社会信用代码").HasMaxLength(50);
        builder.Property(e => e.FTaxNumber).HasColumnName("F纳税人识别号").HasMaxLength(50);
        builder.Property(e => e.FContact).HasColumnName("F联系人").HasMaxLength(100);
        builder.Property(e => e.FPhone).HasColumnName("F电话").HasMaxLength(50);
        builder.Property(e => e.FEmail).HasColumnName("F邮箱").HasMaxLength(100);
        builder.Property(e => e.FAddress).HasColumnName("F地址").HasMaxLength(500);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FCode).IsUnique().HasDatabaseName("IX_SUP供应商_编码");
        builder.HasIndex(e => e.FFullName).HasDatabaseName("IX_SUP供应商_全称");

        builder.HasMany(e => e.BankAccounts)
            .WithOne(e => e.Supplier)
            .HasForeignKey(e => e.FSupplierId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
