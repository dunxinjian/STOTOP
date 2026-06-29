using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Insurance.Entities;

namespace STOTOP.Module.Insurance.Configurations;

public class InsCompanyConfiguration : IEntityTypeConfiguration<InsCompany>
{
    public void Configure(EntityTypeBuilder<InsCompany> builder)
    {
        builder.ToTable("INS保险公司");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCompanyName).HasColumnName("F公司名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FCompanyCode).HasColumnName("F公司编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FCompanyType).HasColumnName("F公司类型").HasDefaultValue(1);
        builder.Property(e => e.FContactPerson).HasColumnName("F联系人").HasMaxLength(50);
        builder.Property(e => e.FContactPhone).HasColumnName("F联系电话").HasMaxLength(50);
        builder.Property(e => e.FAddress).HasColumnName("F地址").HasMaxLength(500);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FOrgId, e.FCompanyCode })
            .IsUnique()
            .HasDatabaseName("IX_INS保险公司_编码");

        builder.HasMany(e => e.Policies)
            .WithOne(e => e.InsuranceCompany)
            .HasForeignKey(e => e.FInsuranceCompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
