using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysPositionDepartmentConfiguration : IEntityTypeConfiguration<SysPositionDepartment>
{
    public void Configure(EntityTypeBuilder<SysPositionDepartment> builder)
    {
        builder.ToTable("SYS岗位组织");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FPositionId).HasColumnName("F岗位ID");
        builder.Property(e => e.FOrganizationId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FPositionId, e.FOrganizationId }).IsUnique().HasDatabaseName("UQ_SYS岗位组织");

        builder.HasOne(e => e.Position)
            .WithMany(p => p.PositionDepartments)
            .HasForeignKey(e => e.FPositionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_岗位组织_岗位");

        builder.HasOne(e => e.Organization)
            .WithMany(d => d.PositionDepartments)
            .HasForeignKey(e => e.FOrganizationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_岗位组织_组织");
    }
}
