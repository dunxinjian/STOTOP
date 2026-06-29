using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpSalesmanAliasConfiguration : IEntityTypeConfiguration<ExpSalesmanAlias>
{
    public void Configure(EntityTypeBuilder<ExpSalesmanAlias> builder)
    {
        builder.ToTable("EXP快递业务员名称映射");

        builder.HasKey(e => e.FID);
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FEmployeeNo).HasColumnName("F员工编号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");

        builder.HasIndex(e => e.FEmployeeNo).HasDatabaseName("IX_快递业务员名称映射_员工编号");
        builder.HasIndex(e => new { e.FName, e.FOrgId }).IsUnique().HasDatabaseName("UQ_快递业务员名称映射_名称组织");
    }
}
