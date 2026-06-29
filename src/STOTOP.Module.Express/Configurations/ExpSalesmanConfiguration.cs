using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpSalesmanConfiguration : IEntityTypeConfiguration<ExpSalesman>
{
    public void Configure(EntityTypeBuilder<ExpSalesman> builder)
    {
        builder.ToTable("EXP业务员");

        builder.HasKey(e => e.FEmployeeNo);
        builder.Property(e => e.FEmployeeNo).HasColumnName("F工号").HasMaxLength(50);
        builder.Property(e => e.FNetworkPointCode).HasColumnName("F网点编号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FEmployeeId).HasColumnName("F员工ID");
        builder.Property(e => e.FName).HasColumnName("F姓名").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FPhone).HasColumnName("F联系电话").HasMaxLength(20);
        builder.Property(e => e.FDepartment).HasColumnName("F所属部门").HasMaxLength(50);
        builder.Property(e => e.FHireDate).HasColumnName("F入职日期");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FNetworkPointCode).HasDatabaseName("IX_EXP业务员_网点编号");
    }
}
