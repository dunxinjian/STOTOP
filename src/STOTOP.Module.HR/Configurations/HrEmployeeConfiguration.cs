using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.HR.Entities;

namespace STOTOP.Module.HR.Configurations;

public class HrEmployeeConfiguration : IEntityTypeConfiguration<HrEmployee>
{
    public void Configure(EntityTypeBuilder<HrEmployee> builder)
    {
        builder.ToTable("HR员工");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(64).IsRequired();
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FName).HasColumnName("F姓名").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FGender).HasColumnName("F性别").HasMaxLength(10);
        builder.Property(e => e.FBirthDate).HasColumnName("F出生日期");
        builder.Property(e => e.FIdCardNumber).HasColumnName("F身份证号").HasMaxLength(20);
        builder.Property(e => e.FPhone).HasColumnName("F手机号").HasMaxLength(20);
        builder.Property(e => e.FEthnicity).HasColumnName("F民族").HasMaxLength(20);
        builder.Property(e => e.FEducation).HasColumnName("F学历").HasMaxLength(20);
        builder.Property(e => e.FMaritalStatus).HasColumnName("F婚姻状况").HasMaxLength(10);
        builder.Property(e => e.FHomeAddress).HasColumnName("F现住地址").HasMaxLength(200);
        builder.Property(e => e.FHouseholdAddress).HasColumnName("F户籍地址").HasMaxLength(200);
        builder.Property(e => e.FEmergencyContact).HasColumnName("F紧急联系人").HasMaxLength(50);
        builder.Property(e => e.FEmergencyContactPhone).HasColumnName("F紧急联系人电话").HasMaxLength(20);
        builder.Property(e => e.FEmergencyContactRelation).HasColumnName("F紧急联系人关系").HasMaxLength(20);
        builder.Property(e => e.FEntryDate).HasColumnName("F入职日期");
        builder.Property(e => e.FRegularDate).HasColumnName("F转正日期");
        builder.Property(e => e.FLeaveDate).HasColumnName("F离职日期");
        builder.Property(e => e.FEmployeeStatus).HasColumnName("F员工状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => e.FUserId).IsUnique();

        builder.HasMany(e => e.PaymentAccounts)
            .WithOne(e => e.Employee)
            .HasForeignKey(e => e.FEmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
