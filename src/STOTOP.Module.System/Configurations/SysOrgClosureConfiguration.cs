using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysOrgClosureConfiguration : IEntityTypeConfiguration<SysOrgClosure>
{
    public void Configure(EntityTypeBuilder<SysOrgClosure> builder)
    {
        builder.ToTable("SYS组织闭包");

        // 复合主键（祖先, 后代）——非 BaseEntity，无 FID。
        builder.HasKey(e => new { e.FAncestorId, e.FDescendantId });

        builder.Property(e => e.FAncestorId).HasColumnName("F祖先ID");
        builder.Property(e => e.FDescendantId).HasColumnName("F后代ID");
        builder.Property(e => e.FDepth).HasColumnName("F层差");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);

        builder.HasIndex(e => e.FDescendantId).HasDatabaseName("IX_SYS组织闭包_后代");
        builder.HasIndex(e => new { e.FAncestorId, e.FDepth }).HasDatabaseName("IX_SYS组织闭包_祖先_层差");
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_SYS组织闭包_租户ID");
    }
}
