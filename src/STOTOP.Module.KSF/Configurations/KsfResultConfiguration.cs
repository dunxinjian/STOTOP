using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.KSF.Entities;

namespace STOTOP.Module.KSF.Configurations;

public class KsfResultConfiguration : IEntityTypeConfiguration<KsfResult>
{
    public void Configure(EntityTypeBuilder<KsfResult> builder)
    {
        builder.ToTable("KSF结果");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F员工ID).HasColumnName("F员工ID");
        builder.Property(e => e.F期间).HasColumnName("F期间").HasMaxLength(6).IsRequired();
        builder.Property(e => e.F方案ID).HasColumnName("F方案ID");
        builder.Property(e => e.F岗位ID快照).HasColumnName("F岗位ID快照");
        builder.Property(e => e.F部门ID快照).HasColumnName("F部门ID快照");
        builder.Property(e => e.F经营单元ID快照).HasColumnName("F经营单元ID快照");
        builder.Property(e => e.F固定部分).HasColumnName("F固定部分").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F浮动部分).HasColumnName("F浮动部分").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F加薪).HasColumnName("F加薪").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F扣减).HasColumnName("F扣减").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F实发).HasColumnName("F实发").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F方案快照JSON).HasColumnName("F方案快照JSON");
        builder.Property(e => e.F状态).HasColumnName("F状态");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F员工ID, e.F期间 }).IsUnique().HasDatabaseName("UQ_KSF结果_员工_期间");
    }
}
